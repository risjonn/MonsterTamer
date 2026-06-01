using SplashKitSDK;
using System.Collections.Generic;

namespace MonsterTamer
{
    public class GameManager
    {
        private static GameManager? _instance = null;

        private Window          _window;
        private GameState       _state;
        private PlayerTrainer   _player;
        private BattleManager?  _battleManager;
        private List<Region>    _regions;
        private int             _currentRegionIndex;
        private OverworldMap?   _overworldMap;

        private GameManager()
        {
            _window             = new Window("Monster Tamer: Guardians of Sarawak", 800, 600);
            _state              = GameState.MainMenu;
            _player             = new PlayerTrainer("Ranger");
            _regions            = new List<Region>();
            _currentRegionIndex = 0;
            _battleManager      = null;
            _overworldMap       = null;

            SetupGame();
        }

        public static GameManager GetInstance()
        {
            if (_instance == null)
                _instance = new GameManager();
            return _instance;
        }

        public GameState      State  => _state;
        public PlayerTrainer  Player => _player;

        private void SetupGame()
        {
            TamedMonster starter = MonsterFactory.CreateKelabit(level: 8);
            _player.Party.AddMonster(starter);

            _player.Bag.AddItem(new Potion("Potion", 30, ItemRarity.Common));
            _player.Bag.AddItem(new Potion("Potion", 30, ItemRarity.Common));
            _player.Bag.AddItem(new CaptureOrb("Capture Orb", 1.0f, ItemRarity.Common));
            _player.Bag.AddItem(new CaptureOrb("Capture Orb", 1.0f, ItemRarity.Common));
            _player.Bag.AddItem(new CaptureOrb("Capture Orb", 1.0f, ItemRarity.Common));

            SetupRegions();
            LoadOverworldMap(0);
        }

        private void SetupRegions()
        {
            Region rainforest = new Region("Rainforest");
            rainforest.AddEncounter(MonsterFactory.CreateBekantan());
            rainforest.AddEncounter(MonsterFactory.CreatePelanduk());
            rainforest.AddEncounter(MonsterFactory.CreateRajah());
            AITrainer t1 = new AITrainer("Ranger Siti", 1);
            t1.Party.AddMonster(MonsterFactory.CreateBekantanTamed(10));
            t1.Party.AddMonster(MonsterFactory.CreatePelandukTamed(9));
            rainforest.AddTrainer(t1);
            _regions.Add(rainforest);

            Region delta = new Region("River Delta");
            delta.AddEncounter(MonsterFactory.CreateBuaya());
            delta.AddEncounter(MonsterFactory.CreateMawas());
            AITrainer t2 = new AITrainer("Ranger Azman", 2);
            t2.Party.AddMonster(MonsterFactory.CreateBuayaTamed(15));
            t2.Party.AddMonster(MonsterFactory.CreateMawasTamed(14));
            delta.AddTrainer(t2);
            _regions.Add(delta);

            Region highlands = new Region("Ancient Highlands");
            highlands.AddEncounter(MonsterFactory.CreateTemaju());
            highlands.AddEncounter(MonsterFactory.CreateEnggang());
            AITrainer t3 = new AITrainer("Champion Dayak", 3);
            t3.Party.AddMonster(MonsterFactory.CreateEnggangTamed(25));
            t3.Party.AddMonster(MonsterFactory.CreateTemajuTamed(24));
            highlands.AddTrainer(t3);
            _regions.Add(highlands);
        }

        private void LoadOverworldMap(int regionIndex)
        {
            Region region = _regions[regionIndex];

            // Build trainer NPCs for the map
            List<TrainerNPC> npcs = new List<TrainerNPC>();
            float npcX = 17 * OverworldMap.TileSize;
            float npcY = 8  * OverworldMap.TileSize;

            foreach (AITrainer t in region.Trainers)
            {
                npcs.Add(new TrainerNPC(npcX, npcY, t, t.Name));
                npcX += OverworldMap.TileSize * 3;
            }

            _overworldMap = regionIndex switch
            {
                0 => MapFactory.CreateRainforestMap(npcs),
                1 => MapFactory.CreateRiverDeltaMap(npcs),
                2 => MapFactory.CreateHighlandsMap(npcs),
                _ => MapFactory.CreateRainforestMap(npcs),
            };
        }

        public void Run()
        {
            SplashKit.CreateTimer("blink");
            SplashKit.StartTimer("blink");

            while (!_window.CloseRequested)
            {
                SplashKit.ProcessEvents();
                HandleInput();
                Update();
                Draw();
            }
        }

        public void HandleInput()
        {
            if (SplashKit.KeyTyped(KeyCode.EscapeKey))
            {
                if (_state == GameState.Battle || _state == GameState.PartyScreen)
                    _state = GameState.Overworld;
                else if (_state == GameState.Overworld)
                    _state = GameState.MainMenu;
                else
                    _window.Close();
                return;
            }

            switch (_state)
            {
                case GameState.MainMenu:
                    if (SplashKit.KeyTyped(KeyCode.ReturnKey) ||
                        SplashKit.KeyTyped(KeyCode.SpaceKey))
                        _state = GameState.Overworld;
                    break;

                case GameState.Battle:
                    HandleBattleInput();
                    break;

                case GameState.PartyScreen:
                    if (SplashKit.KeyTyped(KeyCode.PKey) ||
                        SplashKit.KeyTyped(KeyCode.ReturnKey))
                        _state = GameState.Overworld;
                    break;

                case GameState.Victory:
                case GameState.GameOver:
                    if (SplashKit.KeyTyped(KeyCode.ReturnKey))
                        _window.Close();
                    break;
            }

            // Party screen accessible from overworld too
            if (_state == GameState.Overworld &&
                SplashKit.KeyTyped(KeyCode.PKey))
                _state = GameState.PartyScreen;
        }

        private void HandleBattleInput()
        {
            if (_battleManager == null) return;

            if (SplashKit.KeyTyped(KeyCode.LeftKey))  _player.SelectedMoveIndex--;
            if (SplashKit.KeyTyped(KeyCode.RightKey)) _player.SelectedMoveIndex++;
            if (SplashKit.KeyTyped(KeyCode.UpKey))    _player.SelectedMoveIndex -= 2;
            if (SplashKit.KeyTyped(KeyCode.DownKey))  _player.SelectedMoveIndex += 2;

            if (SplashKit.KeyTyped(KeyCode.ReturnKey))
            {
                _battleManager.PlayerTurn(_player.SelectedMoveIndex);
                if (!_battleManager.IsBattleOver())
                    _battleManager.EnemyTurn();
                if (_battleManager.IsBattleOver())
                    EndBattle();
            }
        }

        public void Update()
        {
            if (_state == GameState.Overworld && _overworldMap != null)
            {
                // Support both arrow keys and WASD for movement
                bool up    = SplashKit.KeyDown(KeyCode.UpKey)    || SplashKit.KeyDown(KeyCode.WKey);
                bool down  = SplashKit.KeyDown(KeyCode.DownKey)  || SplashKit.KeyDown(KeyCode.SKey);
                bool left  = SplashKit.KeyDown(KeyCode.LeftKey)  || SplashKit.KeyDown(KeyCode.AKey);
                bool right = SplashKit.KeyDown(KeyCode.RightKey) || SplashKit.KeyDown(KeyCode.DKey);

                _overworldMap.Update(up, down, left, right);

                if (_overworldMap.WildEncounterTriggered)
                {
                    WildMonster? encounter = _regions[_currentRegionIndex].GetRandomEncounter();
                    if (encounter != null) StartWildBattle(encounter);
                }
                else if (_overworldMap.TrainerEncounterTriggered &&
                         _overworldMap.TriggeredTrainer != null)
                {
                    StartTrainerBattle(_overworldMap.TriggeredTrainer);
                }
                else if (_overworldMap.RegionExitTriggered)
                {
                    AdvanceRegion();
                }
            }
        }

        public void Draw()
        {
            SplashKit.ClearScreen(SplashKit.RGBColor(15, 25, 12));

            switch (_state)
            {
                case GameState.MainMenu:    DrawMainMenu();    break;
                case GameState.Overworld:   DrawOverworld();   break;
                case GameState.Battle:      DrawBattle();      break;
                case GameState.PartyScreen: DrawPartyScreen(); break;
                case GameState.Victory:     DrawVictory();     break;
                case GameState.GameOver:    DrawGameOver();    break;
            }

            SplashKit.RefreshScreen(60);
        }

        private void StartWildBattle(WildMonster wild)
        {
            AITrainer dummy     = new AITrainer("Wild");
            _battleManager      = new BattleManager(_player, dummy);
            _battleManager.WildTarget = wild;
            _battleManager.AddLog($"A wild {wild.Name} appeared!");
            _battleManager.AddLog($"{wild.GetBattleCry()}");
            _battleManager.AddLog($"Go, {_player.GetActiveMonster()?.Name}!");
            _state = GameState.Battle;
        }

        private void StartTrainerBattle(AITrainer trainer)
        {
            _battleManager = new BattleManager(_player, trainer);
            _battleManager.AddLog($"Trainer {trainer.Name} wants to battle!");
            _battleManager.AddLog($"Go, {_player.GetActiveMonster()?.Name}!");
            _state = GameState.Battle;
        }

        private void AdvanceRegion()
        {
            _regions[_currentRegionIndex].Clear();
            if (_currentRegionIndex < _regions.Count - 1)
            {
                _currentRegionIndex++;
                LoadOverworldMap(_currentRegionIndex);
            }
            else
            {
                // All regions cleared — final boss
                StartBossBattle();
            }
        }

        private void StartBossBattle()
        {
            AITrainer bossTrainer = new AITrainer("Naga Belian", 3);
            TamedMonster boss = MonsterFactory.CreateKelabit(30);
            bossTrainer.Party.AddMonster(boss);
            _battleManager = new BattleManager(_player, bossTrainer);
            _battleManager.AddLog("NAGA BELIAN EMERGES FROM THE DEPTHS!");
            _battleManager.AddLog("The ancient sea dragon awakens...");
            _state = GameState.Battle;
        }

        private void EndBattle()
        {
            if (_battleManager == null) return;
            if (_battleManager.Result == BattleResult.PlayerDied)
            {
                _state = GameState.GameOver;
                return;
            }
            if (_battleManager.Result == BattleResult.PlayerWon)
            {
                // Check if all trainers defeated — clear region
                bool allDefeated = true;
                foreach (AITrainer t in _regions[_currentRegionIndex].Trainers)
                    if (!t.Party.IsWiped()) { allDefeated = false; break; }
                if (allDefeated)
                    _regions[_currentRegionIndex].Clear();
            }
            _state = GameState.Overworld;
        }

        // ── Draw methods ────────────────────────────────────────

        private void DrawMainMenu()
        {
            // Sky gradient simulation
            SplashKit.FillRectangle(SplashKit.RGBColor(10, 40, 20),   0,   0, 800, 300);
            SplashKit.FillRectangle(SplashKit.RGBColor(15, 55, 25),   0, 300, 800, 300);

            // Jungle silhouette
            SplashKit.FillEllipse(SplashKit.RGBColor(8, 30, 8),   20, 260, 200, 120);
            SplashKit.FillEllipse(SplashKit.RGBColor(8, 30, 8),  150, 240, 260, 140);
            SplashKit.FillEllipse(SplashKit.RGBColor(8, 30, 8),  380, 250, 220, 130);
            SplashKit.FillEllipse(SplashKit.RGBColor(8, 30, 8),  560, 235, 280, 150);
            SplashKit.FillRectangle(SplashKit.RGBColor(8, 30, 8), 0, 310, 800, 50);

            // Stars
            for (int i = 0; i < 40; i++)
            {
                int sx = (i * 97 + 13) % 780 + 10;
                int sy = (i * 53 + 7)  % 140 + 10;
                SplashKit.FillCircle(SplashKit.RGBColor(255, 255, 200), sx, sy, 1);
            }

            // Title
            SplashKit.DrawText("Monster Tamer",
                SplashKit.RGBColor(255, 215, 0), "Arial", 44, 185, 140);
            SplashKit.DrawText("Guardians of Sarawak",
                SplashKit.RGBColor(116, 198, 157), "Arial", 22, 230, 198);

            // Divider
            SplashKit.DrawLine(SplashKit.RGBColor(45, 106, 79), 180, 228, 620, 228);

            // Monster list
            string[] names = { "Kelabit","Bekantan","Mawas","Rajah",
                                "Enggang","Pelanduk","Temaju","Buaya" };
            for (int i = 0; i < names.Length; i++)
            {
                int col = i % 4, row = i / 4;
                int tx = 200 + col * 110, ty = 240 + row * 22;
                SplashKit.DrawText(names[i],
                    SplashKit.RGBColor(170, 220, 170), "Arial", 13, tx, ty);
            }
            SplashKit.DrawText("+ Naga Belian (Final Boss)",
                SplashKit.RGBColor(255, 215, 0), "Arial", 13, 290, 286);

            // Start prompt
            bool blink = (SplashKit.TimerTicks("blink") % 900) < 600;
            if (blink)
            {
                SplashKit.FillRectangle(
                    SplashKit.RGBColor(26, 71, 49), 270, 318, 260, 32);
                SplashKit.DrawText("Press Enter to Start",
                    SplashKit.RGBColor(116, 198, 157), "Arial", 14, 305, 328);
            }

            SplashKit.DrawText("Option 4  ·  C# with SplashKit",
                SplashKit.RGBColor(45, 106, 79), "Arial", 10, 295, 572);
        }

        private void DrawOverworld()
        {
            if (_overworldMap != null)
                _overworldMap.Draw(_window);
        }

        private void DrawBattle()
        {
            if (_battleManager == null) return;

            TamedMonster? playerMon = _player.GetActiveMonster();
            Monster? enemyMon = (Monster?)_battleManager.WildTarget
                             ?? _battleManager.Opponent.GetActiveMonster();

            // Sky background
            SplashKit.FillRectangle(SplashKit.RGBColor(100, 180, 240), 0,   0, 800, 320);
            SplashKit.FillRectangle(SplashKit.RGBColor(60, 140, 60),   0, 280, 800, 320);

            // Clouds
            SplashKit.FillEllipse(SplashKit.RGBColor(230, 240, 255), 100, 40, 120, 40);
            SplashKit.FillEllipse(SplashKit.RGBColor(230, 240, 255), 130, 30, 80,  30);
            SplashKit.FillEllipse(SplashKit.RGBColor(230, 240, 255), 500, 55, 110, 38);
            SplashKit.FillEllipse(SplashKit.RGBColor(230, 240, 255), 530, 44, 75,  28);

            // Jungle silhouette
            SplashKit.FillEllipse(SplashKit.RGBColor(25, 80, 15),  20, 200, 160, 100);
            SplashKit.FillEllipse(SplashKit.RGBColor(25, 80, 15), 130, 185, 200, 110);
            SplashKit.FillEllipse(SplashKit.RGBColor(25, 80, 15), 450, 193, 180, 105);
            SplashKit.FillEllipse(SplashKit.RGBColor(25, 80, 15), 600, 183, 210, 115);
            SplashKit.FillRectangle(SplashKit.RGBColor(25, 80, 15), 0, 250, 800, 80);

            // Enemy platform
            SplashKit.FillEllipse(SplashKit.RGBColor(40, 100, 25), 440, 155, 220, 44);
            SplashKit.DrawEllipse(SplashKit.RGBColor(60, 130, 40), 440, 155, 220, 44);

            // Player platform
            SplashKit.FillEllipse(SplashKit.RGBColor(35, 90, 20),  80, 290, 240, 50);
            SplashKit.DrawEllipse(SplashKit.RGBColor(55, 120, 35),  80, 290, 240, 50);

            // Draw monster sprites
            if (enemyMon != null)
                DrawMonsterSprite(enemyMon.Name, 550, 130, false);
            if (playerMon != null)
                DrawMonsterSprite(playerMon.Name, 175, 250, true);

            // Enemy info panel
            if (enemyMon != null)
                DrawInfoPanel(enemyMon, 16, 16, 280, 88, false);

            // Player info panel
            if (playerMon != null)
                DrawInfoPanel(playerMon, 504, 330, 280, 88, true);

            // Move panel
            if (playerMon != null)
                DrawMovePanel(playerMon);

            // Battle log
            DrawBattleLog();

            // Controls hint
            SplashKit.DrawText(
                "Arrows: select move     Enter: use move     Esc: flee",
                SplashKit.RGBColor(80, 80, 80), "Arial", 10, 20, 582);
        }

        private void DrawMonsterSprite(string name, float cx, float cy, bool isPlayer)
        {
            float scale = isPlayer ? 1.1f : 1.0f;

            switch (name)
            {
                case "Kelabit": DrawKelabit(cx, cy, scale, isPlayer); break;
                case "Bekantan": DrawBekantan(cx, cy, scale); break;
                case "Mawas": DrawMawas(cx, cy, scale); break;
                case "Rajah": DrawRajah(cx, cy, scale); break;
                case "Enggang": DrawEnggang(cx, cy, scale); break;
                case "Pelanduk": DrawPelanduk(cx, cy, scale); break;
                case "Temaju": DrawTemaju(cx, cy, scale); break;
                case "Buaya": DrawBuaya(cx, cy, scale); break;
                default: DrawGenericMonster(cx, cy, scale); break;
            }

            // Shadow
            SplashKit.FillEllipse(SplashKit.RGBAColor(0, 0, 0, 60),
                cx - 30 * scale, cy + 10, 60 * scale, 14);
        }

        private void DrawKelabit(float cx, float cy, float s, bool back)
        {
            // Wings
            SplashKit.FillEllipse(SplashKit.RGBColor(30, 90, 30),
                cx - 55*s, cy - 45*s, 50*s, 70*s);
            SplashKit.FillEllipse(SplashKit.RGBColor(30, 90, 30),
                cx + 5*s,  cy - 45*s, 50*s, 70*s);
            // Body
            SplashKit.FillEllipse(SplashKit.RGBColor(60, 140, 60),
                cx - 28*s, cy - 55*s, 56*s, 75*s);
            // Head
            SplashKit.FillCircle(SplashKit.RGBColor(60, 140, 60), cx, cy - 65*s, 22*s);
            // Casque (hornbill beak ridge)
            SplashKit.FillRectangle(SplashKit.RGBColor(255, 165, 0),
                cx - 5*s, cy - 88*s, 38*s, 10*s);
            SplashKit.FillRectangle(SplashKit.RGBColor(255, 200, 50),
                cx - 4*s, cy - 82*s, 36*s, 8*s);
            // Beak
            SplashKit.FillTriangle(SplashKit.RGBColor(255, 140, 0),
                cx - 2*s, cy - 72*s,
                cx + 32*s, cy - 68*s,
                cx - 2*s, cy - 62*s);
            // Eyes
            if (!back)
            {
                SplashKit.FillCircle(SplashKit.ColorWhite(), cx - 8*s, cy - 68*s, 5*s);
                SplashKit.FillCircle(SplashKit.ColorBlack(), cx - 8*s, cy - 68*s, 3*s);
                SplashKit.FillCircle(SplashKit.ColorWhite(), cx + 6*s, cy - 70*s, 4*s);
                SplashKit.FillCircle(SplashKit.ColorBlack(), cx + 6*s, cy - 70*s, 2*s);
            }
            // Feet
            SplashKit.FillRectangle(SplashKit.RGBColor(200, 150, 0),
                cx - 12*s, cy + 18*s, 8*s, 10*s);
            SplashKit.FillRectangle(SplashKit.RGBColor(200, 150, 0),
                cx + 4*s, cy + 18*s, 8*s, 10*s);
        }

        private void DrawBekantan(float cx, float cy, float s)
        {
            // Body
            SplashKit.FillEllipse(SplashKit.RGBColor(180, 120, 60),
                cx - 25*s, cy - 55*s, 50*s, 75*s);
            // Arms
            SplashKit.FillEllipse(SplashKit.RGBColor(160, 100, 50),
                cx - 50*s, cy - 40*s, 28*s, 55*s);
            SplashKit.FillEllipse(SplashKit.RGBColor(160, 100, 50),
                cx + 22*s, cy - 40*s, 28*s, 55*s);
            // Head
            SplashKit.FillCircle(SplashKit.RGBColor(180, 120, 60), cx, cy - 68*s, 24*s);
            // Big nose (proboscis)
            SplashKit.FillEllipse(SplashKit.RGBColor(220, 160, 100),
                cx - 10*s, cy - 72*s, 22*s, 28*s);
            SplashKit.FillEllipse(SplashKit.RGBColor(200, 140, 80),
                cx - 8*s, cy - 70*s, 18*s, 22*s);
            // Eyes
            SplashKit.FillCircle(SplashKit.ColorWhite(), cx - 10*s, cy - 76*s, 5*s);
            SplashKit.FillCircle(SplashKit.ColorBlack(), cx - 10*s, cy - 76*s, 3*s);
            SplashKit.FillCircle(SplashKit.ColorWhite(), cx + 8*s,  cy - 76*s, 5*s);
            SplashKit.FillCircle(SplashKit.ColorBlack(), cx + 8*s,  cy - 76*s, 3*s);
            // Legs
            SplashKit.FillRectangle(SplashKit.RGBColor(160, 100, 50),
                cx - 20*s, cy + 18*s, 14*s, 14*s);
            SplashKit.FillRectangle(SplashKit.RGBColor(160, 100, 50),
                cx + 6*s, cy + 18*s, 14*s, 14*s);
        }

        private void DrawMawas(float cx, float cy, float s)
        {
            // Body — large
            SplashKit.FillEllipse(SplashKit.RGBColor(160, 90, 30),
                cx - 32*s, cy - 60*s, 64*s, 85*s);
            // Long arms
            SplashKit.FillEllipse(SplashKit.RGBColor(140, 75, 20),
                cx - 60*s, cy - 50*s, 32*s, 70*s);
            SplashKit.FillEllipse(SplashKit.RGBColor(140, 75, 20),
                cx + 28*s, cy - 50*s, 32*s, 70*s);
            // Head
            SplashKit.FillCircle(SplashKit.RGBColor(160, 90, 30), cx, cy - 72*s, 26*s);
            // Face disc
            SplashKit.FillEllipse(SplashKit.RGBColor(200, 140, 80),
                cx - 14*s, cy - 80*s, 28*s, 24*s);
            // Eyes
            SplashKit.FillCircle(SplashKit.ColorBlack(), cx - 6*s,  cy - 72*s, 4*s);
            SplashKit.FillCircle(SplashKit.ColorBlack(), cx + 6*s,  cy - 72*s, 4*s);
            SplashKit.FillCircle(SplashKit.ColorWhite(), cx - 7*s,  cy - 73*s, 2*s);
            SplashKit.FillCircle(SplashKit.ColorWhite(), cx + 5*s,  cy - 73*s, 2*s);
        }

        private void DrawRajah(float cx, float cy, float s)
        {
            // Wings — large and colourful
            SplashKit.FillEllipse(SplashKit.RGBColor(0, 0, 0),
                cx - 55*s, cy - 30*s, 50*s, 35*s);
            SplashKit.FillEllipse(SplashKit.RGBColor(0, 0, 0),
                cx + 5*s,  cy - 30*s, 50*s, 35*s);
            // Wing colour patches
            SplashKit.FillEllipse(SplashKit.RGBColor(0, 200, 100),
                cx - 48*s, cy - 28*s, 35*s, 22*s);
            SplashKit.FillEllipse(SplashKit.RGBColor(0, 200, 100),
                cx + 12*s, cy - 28*s, 35*s, 22*s);
            SplashKit.FillEllipse(SplashKit.RGBColor(255, 215, 0),
                cx - 40*s, cy - 24*s, 18*s, 12*s);
            SplashKit.FillEllipse(SplashKit.RGBColor(255, 215, 0),
                cx + 22*s, cy - 24*s, 18*s, 12*s);
            // Body
            SplashKit.FillEllipse(SplashKit.RGBColor(20, 20, 20),
                cx - 10*s, cy - 26*s, 20*s, 36*s);
            // Head/antenna
            SplashKit.FillCircle(SplashKit.RGBColor(20, 20, 20), cx, cy - 32*s, 10*s);
            SplashKit.DrawLine(SplashKit.RGBColor(20, 20, 20),
                cx - 4*s, cy - 40*s, cx - 10*s, cy - 52*s);
            SplashKit.DrawLine(SplashKit.RGBColor(20, 20, 20),
                cx + 4*s, cy - 40*s, cx + 10*s, cy - 52*s);
            SplashKit.FillCircle(SplashKit.RGBColor(255, 215, 0), cx - 10*s, cy - 52*s, 3*s);
            SplashKit.FillCircle(SplashKit.RGBColor(255, 215, 0), cx + 10*s, cy - 52*s, 3*s);
            // Eyes
            SplashKit.FillCircle(SplashKit.RGBColor(255, 215, 0), cx - 4*s, cy - 33*s, 3*s);
            SplashKit.FillCircle(SplashKit.RGBColor(255, 215, 0), cx + 4*s, cy - 33*s, 3*s);
        }

        private void DrawEnggang(float cx, float cy, float s)
        {
            // Body — stocky bear
            SplashKit.FillEllipse(SplashKit.RGBColor(40, 30, 20),
                cx - 30*s, cy - 58*s, 60*s, 80*s);
            // Arms
            SplashKit.FillEllipse(SplashKit.RGBColor(35, 25, 15),
                cx - 52*s, cy - 45*s, 25*s, 50*s);
            SplashKit.FillEllipse(SplashKit.RGBColor(35, 25, 15),
                cx + 27*s, cy - 45*s, 25*s, 50*s);
            // Head
            SplashKit.FillCircle(SplashKit.RGBColor(40, 30, 20), cx, cy - 68*s, 26*s);
            // Chest patch (sun bear yellow)
            SplashKit.FillEllipse(SplashKit.RGBColor(255, 200, 50),
                cx - 14*s, cy - 52*s, 28*s, 22*s);
            // Ears
            SplashKit.FillCircle(SplashKit.RGBColor(40, 30, 20), cx - 20*s, cy - 88*s, 10*s);
            SplashKit.FillCircle(SplashKit.RGBColor(40, 30, 20), cx + 20*s, cy - 88*s, 10*s);
            // Eyes
            SplashKit.FillCircle(SplashKit.ColorBlack(), cx - 8*s, cy - 70*s, 5*s);
            SplashKit.FillCircle(SplashKit.ColorBlack(), cx + 8*s, cy - 70*s, 5*s);
            SplashKit.FillCircle(SplashKit.ColorWhite(), cx - 9*s, cy - 71*s, 2*s);
            SplashKit.FillCircle(SplashKit.ColorWhite(), cx + 7*s, cy - 71*s, 2*s);
            // Snout
            SplashKit.FillEllipse(SplashKit.RGBColor(80, 60, 40),
                cx - 8*s, cy - 62*s, 16*s, 10*s);
        }

        private void DrawPelanduk(float cx, float cy, float s)
        {
            // Legs — long and spindly
            SplashKit.FillRectangle(SplashKit.RGBColor(140, 100, 60),
                cx - 14*s, cy - 20*s, 6*s, 28*s);
            SplashKit.FillRectangle(SplashKit.RGBColor(140, 100, 60),
                cx + 8*s,  cy - 20*s, 6*s, 28*s);
            // Body
            SplashKit.FillEllipse(SplashKit.RGBColor(160, 120, 70),
                cx - 20*s, cy - 52*s, 40*s, 40*s);
            // Head — small
            SplashKit.FillCircle(SplashKit.RGBColor(160, 120, 70), cx, cy - 60*s, 16*s);
            // Ears — tall
            SplashKit.FillEllipse(SplashKit.RGBColor(160, 120, 70),
                cx - 12*s, cy - 78*s, 8*s, 20*s);
            SplashKit.FillEllipse(SplashKit.RGBColor(160, 120, 70),
                cx + 4*s,  cy - 78*s, 8*s, 20*s);
            SplashKit.FillEllipse(SplashKit.RGBColor(220, 160, 140),
                cx - 11*s, cy - 77*s, 6*s, 16*s);
            SplashKit.FillEllipse(SplashKit.RGBColor(220, 160, 140),
                cx + 5*s,  cy - 77*s, 6*s, 16*s);
            // Eyes
            SplashKit.FillCircle(SplashKit.ColorBlack(), cx - 5*s, cy - 62*s, 4*s);
            SplashKit.FillCircle(SplashKit.ColorBlack(), cx + 5*s, cy - 62*s, 4*s);
            SplashKit.FillCircle(SplashKit.ColorWhite(), cx - 6*s, cy - 63*s, 2*s);
            SplashKit.FillCircle(SplashKit.ColorWhite(), cx + 4*s, cy - 63*s, 2*s);
            // Tiny horns
            SplashKit.FillRectangle(SplashKit.RGBColor(180, 140, 80),
                cx - 4*s, cy - 74*s, 3*s, 8*s);
            SplashKit.FillRectangle(SplashKit.RGBColor(180, 140, 80),
                cx + 2*s, cy - 74*s, 3*s, 8*s);
        }

        private void DrawTemaju(float cx, float cy, float s)
        {
            // Rolled up pangolin shape
            SplashKit.FillEllipse(SplashKit.RGBColor(120, 100, 70),
                cx - 28*s, cy - 50*s, 56*s, 65*s);
            // Scales — layered triangles
            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    float sx2 = cx - 22*s + col * 13*s;
                    float sy2 = cy - 44*s + row * 13*s;
                    SplashKit.FillTriangle(
                        SplashKit.RGBColor(150, 120, 80),
                        sx2, sy2 + 12*s,
                        sx2 + 6*s, sy2,
                        sx2 + 12*s, sy2 + 12*s);
                    SplashKit.DrawTriangle(
                        SplashKit.RGBColor(100, 80, 50),
                        sx2, sy2 + 12*s,
                        sx2 + 6*s, sy2,
                        sx2 + 12*s, sy2 + 12*s);
                }
            }
            // Head peeking out
            SplashKit.FillCircle(SplashKit.RGBColor(130, 110, 75),
                cx + 20*s, cy - 30*s, 16*s);
            SplashKit.FillEllipse(SplashKit.RGBColor(110, 90, 60),
                cx + 24*s, cy - 28*s, 16*s, 8*s);
            // Eye
            SplashKit.FillCircle(SplashKit.ColorBlack(), cx + 22*s, cy - 32*s, 3*s);
        }

        private void DrawBuaya(float cx, float cy, float s)
        {
            // Tail
            SplashKit.FillEllipse(SplashKit.RGBColor(60, 100, 50),
                cx - 60*s, cy - 15*s, 60*s, 22*s);
            // Body
            SplashKit.FillEllipse(SplashKit.RGBColor(70, 115, 55),
                cx - 30*s, cy - 28*s, 70*s, 42*s);
            // Legs
            SplashKit.FillEllipse(SplashKit.RGBColor(60, 100, 50),
                cx - 28*s, cy - 5*s, 20*s, 16*s);
            SplashKit.FillEllipse(SplashKit.RGBColor(60, 100, 50),
                cx + 8*s,  cy - 5*s, 20*s, 16*s);
            // Head + long snout
            SplashKit.FillEllipse(SplashKit.RGBColor(70, 115, 55),
                cx + 26*s, cy - 26*s, 48*s, 22*s);
            // Teeth
            for (int i = 0; i < 5; i++)
            {
                SplashKit.FillTriangle(SplashKit.ColorWhite(),
                    cx + 28*s + i*6*s, cy - 14*s,
                    cx + 31*s + i*6*s, cy - 8*s,
                    cx + 34*s + i*6*s, cy - 14*s);
            }
            // Eye
            SplashKit.FillCircle(SplashKit.RGBColor(255, 215, 0),
                cx + 32*s, cy - 26*s, 6*s);
            SplashKit.FillCircle(SplashKit.ColorBlack(),
                cx + 32*s, cy - 26*s, 3*s);
            // Scales on back
            for (int i = 0; i < 5; i++)
            {
                SplashKit.FillTriangle(
                    SplashKit.RGBColor(50, 85, 40),
                    cx - 20*s + i*12*s, cy - 28*s,
                    cx - 14*s + i*12*s, cy - 38*s,
                    cx - 8*s  + i*12*s, cy - 28*s);
            }
        }

        private void DrawGenericMonster(float cx, float cy, float s)
        {
            SplashKit.FillCircle(SplashKit.RGBColor(100, 160, 100), cx, cy - 30*s, 30*s);
            SplashKit.FillCircle(SplashKit.ColorWhite(), cx - 10*s, cy - 34*s, 8*s);
            SplashKit.FillCircle(SplashKit.ColorBlack(), cx - 10*s, cy - 34*s, 5*s);
            SplashKit.FillCircle(SplashKit.ColorWhite(), cx + 10*s, cy - 34*s, 8*s);
            SplashKit.FillCircle(SplashKit.ColorBlack(), cx + 10*s, cy - 34*s, 5*s);
        }

        private void DrawInfoPanel(Monster m, int x, int y, int w, int h, bool isPlayer)
        {
            // Panel background
            SplashKit.FillRectangle(SplashKit.RGBAColor(20, 35, 15, 230), x, y, w, h);
            SplashKit.DrawRectangle(SplashKit.RGBColor(80, 180, 80), x, y, w, h);

            // Name + level
            SplashKit.DrawText(m.Name,
                SplashKit.ColorWhite(), "Arial", 15, x + 10, y + 10);
            SplashKit.DrawText($"Lv.{m.Level}",
                SplashKit.RGBColor(170, 220, 170), "Arial", 11, x + w - 44, y + 13);

            // Type badges
            DrawTypeBadge(m.PrimaryType, x + 10, y + 30);
            if (m.HasDualType)
                DrawTypeBadge(m.SecondaryType, x + 74, y + 30);

            // HP bar
            int barW = w - 20;
            SplashKit.FillRectangle(SplashKit.RGBColor(40, 40, 40), x + 10, y + 52, barW, 10);
            float pct  = m.HpPercent;
            Color barC = pct > 0.5f ? SplashKit.RGBColor(80, 220, 80)
                       : pct > 0.25f ? SplashKit.RGBColor(240, 210, 40)
                       : SplashKit.RGBColor(240, 60, 60);
            int fillW  = (int)(barW * pct);
            if (fillW > 0)
                SplashKit.FillRectangle(barC, x + 10, y + 52, fillW, 10);
            SplashKit.DrawRectangle(SplashKit.RGBColor(80, 80, 80), x + 10, y + 52, barW, 10);
            SplashKit.DrawText($"HP  {m.Hp} / {m.MaxHp}",
                SplashKit.RGBColor(180, 180, 180), "Arial", 10, x + 10, y + 65);

            // Status condition badge if any
            if (m.Status != StatusCondition.None)
            {
                SplashKit.FillRectangle(SplashKit.RGBColor(180, 60, 180),
                    x + w - 70, y + 62, 58, 16);
                SplashKit.DrawText(m.Status.ToString(),
                    SplashKit.ColorWhite(), "Arial", 9, x + w - 68, y + 65);
            }
        }

        private void DrawTypeBadge(MonsterType t, int x, int y)
        {
            Color bg = TypeBadgeColor(t);
            SplashKit.FillRectangle(bg, x, y, 58, 16);
            SplashKit.DrawText(t.ToString(),
                SplashKit.ColorWhite(), "Arial", 10, x + 5, y + 3);
        }

        private Color TypeBadgeColor(MonsterType t) => t switch
        {
            MonsterType.Fire     => SplashKit.RGBColor(220, 80,  40),
            MonsterType.Water    => SplashKit.RGBColor(60,  130, 220),
            MonsterType.Grass    => SplashKit.RGBColor(60,  170, 70),
            MonsterType.Electric => SplashKit.RGBColor(220, 190, 40),
            MonsterType.Flying   => SplashKit.RGBColor(140, 100, 220),
            MonsterType.Fighting => SplashKit.RGBColor(180, 60,  60),
            MonsterType.Steel    => SplashKit.RGBColor(160, 160, 180),
            _                    => SplashKit.RGBColor(160, 155, 140),
        };

        private void DrawMovePanel(TamedMonster player)
        {
            SplashKit.FillRectangle(SplashKit.RGBAColor(20, 35, 15, 230), 16, 336, 440, 200);
            SplashKit.DrawRectangle(SplashKit.RGBColor(80, 180, 80), 16, 336, 440, 200);
            SplashKit.DrawText("Choose a move:",
                SplashKit.RGBColor(180, 220, 180), "Arial", 11, 26, 346);

            for (int i = 0; i < player.MoveSet.Length; i++)
            {
                int col = i % 2, row = i / 2;
                int bx  = 26 + col * 212, by = 362 + row * 84;
                bool sel = (i == _player.SelectedMoveIndex);

                SplashKit.FillRectangle(
                    sel ? SplashKit.RGBColor(50, 100, 40)
                        : SplashKit.RGBColor(25, 55, 20),
                    bx, by, 200, 72);
                SplashKit.DrawRectangle(
                    sel ? SplashKit.RGBColor(255, 215, 0)
                        : SplashKit.RGBColor(80, 160, 60),
                    bx, by, 200, 72);

                Move move = player.MoveSet[i];
                Color nameCol = sel ? SplashKit.RGBColor(255, 215, 0)
                                    : SplashKit.ColorWhite();
                SplashKit.DrawText(move.Name,     nameCol, "Arial", 13, bx + 10, by + 10);
                SplashKit.DrawText(move.GetMoveCategory(),
                    SplashKit.RGBColor(160, 200, 160), "Arial", 11, bx + 10, by + 30);
                DrawTypeBadge(move.MoveType, bx + 10, by + 48);
                if (move.Power > 0)
                    SplashKit.DrawText($"PWR {move.Power}",
                        SplashKit.RGBColor(140, 180, 140), "Arial", 10, bx + 158, by + 52);
            }
        }

        private void DrawBattleLog()
        {
            if (_battleManager == null) return;
            SplashKit.FillRectangle(SplashKit.RGBAColor(10, 20, 8, 230), 464, 436, 320, 100);
            SplashKit.DrawRectangle(SplashKit.RGBColor(60, 140, 60), 464, 436, 320, 100);
            SplashKit.DrawText("Battle log",
                SplashKit.RGBColor(80, 130, 80), "Arial", 10, 474, 441);

            List<string> log = _battleManager.BattleLog;
            int start        = System.Math.Max(0, log.Count - 4);
            for (int i = start; i < log.Count; i++)
            {
                string entry = log[i].Length > 44 ? log[i].Substring(0, 44) + "..." : log[i];
                float alpha  = 0.5f + (float)(i - start) / 4f * 0.5f;
                int   a      = (int)(alpha * 255);
                SplashKit.DrawText(entry,
                    SplashKit.RGBAColor(200, 220, 200, a), "Arial", 10,
                    474, 455 + (i - start) * 20);
            }
        }

        private void DrawPartyScreen()
        {
            SplashKit.FillRectangle(SplashKit.RGBColor(10, 30, 10), 0, 0, 800, 600);

            SplashKit.DrawText("Party",
                SplashKit.RGBColor(255, 215, 0), "Arial", 24, 360, 28);
            SplashKit.DrawLine(SplashKit.RGBColor(45, 106, 79), 60, 56, 740, 56);

            for (int i = 0; i < _player.Party.Count; i++)
            {
                TamedMonster m = _player.Party[i];
                int py = 68 + i * 82;

                SplashKit.FillRectangle(
                    SplashKit.RGBColor(18, 45, 18), 60, py, 680, 72);
                SplashKit.DrawRectangle(
                    SplashKit.RGBColor(80, 160, 80), 60, py, 680, 72);

                // Monster sprite (small)
                DrawMonsterSprite(m.Name, 110, py + 40, false);

                SplashKit.DrawText($"{i+1}.  {m.Name}",
                    SplashKit.ColorWhite(), "Arial", 16, 160, py + 12);
                SplashKit.DrawText($"Lv.{m.Level}",
                    SplashKit.RGBColor(116, 198, 157), "Arial", 13, 160, py + 34);
                SplashKit.DrawText($"EXP: {m.Experience}",
                    SplashKit.RGBColor(116, 198, 157), "Arial", 12, 240, py + 34);
                SplashKit.DrawText(m.GetDescription(),
                    SplashKit.RGBColor(120, 160, 120), "Arial", 11, 160, py + 54);

                // HP bar
                SplashKit.FillRectangle(SplashKit.RGBColor(40, 40, 40), 420, py + 20, 280, 12);
                float pct  = m.HpPercent;
                Color barC = pct > 0.5f ? SplashKit.RGBColor(80, 220, 80)
                           : pct > 0.25f ? SplashKit.RGBColor(240, 210, 40)
                           : SplashKit.RGBColor(240, 60, 60);
                SplashKit.FillRectangle(barC, 420, py + 20, (int)(280 * pct), 12);
                SplashKit.DrawRectangle(SplashKit.RGBColor(80, 80, 80), 420, py + 20, 280, 12);
                SplashKit.DrawText($"HP  {m.Hp} / {m.MaxHp}",
                    SplashKit.RGBColor(160, 200, 160), "Arial", 11, 420, py + 38);

                // Moves
                for (int mi = 0; mi < m.MoveSet.Length; mi++)
                {
                    int mx = 420 + (mi % 2) * 145;
                    int my = py + 52 + (mi / 2) * 0;
                    SplashKit.DrawText($"· {m.MoveSet[mi].Name}",
                        SplashKit.RGBColor(140, 180, 140), "Arial", 10, mx, my);
                }
            }

            SplashKit.DrawText("P or Enter to return",
                SplashKit.RGBColor(100, 150, 100), "Arial", 12, 330, 564);
        }

        private void DrawVictory()
        {
            SplashKit.FillRectangle(SplashKit.RGBColor(5, 20, 5), 0, 0, 800, 600);
            SplashKit.FillEllipse(SplashKit.RGBAColor(255, 215, 0, 30), 300, 200, 200, 200);
            SplashKit.DrawText("Victory!",
                SplashKit.RGBColor(255, 215, 0), "Arial", 56, 255, 210);
            SplashKit.DrawText("You are the Guardian of Sarawak!",
                SplashKit.ColorWhite(), "Arial", 20, 195, 290);
            SplashKit.DrawText("Naga Belian has been defeated.",
                SplashKit.RGBColor(116, 198, 157), "Arial", 14, 280, 322);
            SplashKit.DrawText("Press Enter to exit",
                SplashKit.RGBColor(100, 150, 100), "Arial", 12, 320, 390);
        }

        private void DrawGameOver()
        {
            SplashKit.FillRectangle(SplashKit.RGBColor(20, 5, 5), 0, 0, 800, 600);
            SplashKit.DrawText("Defeated!",
                SplashKit.RGBColor(220, 60, 60), "Arial", 56, 250, 210);
            SplashKit.DrawText("All your monsters have fainted...",
                SplashKit.RGBColor(200, 180, 180), "Arial", 18, 225, 290);
            SplashKit.DrawText("Press Enter to exit",
                SplashKit.RGBColor(120, 100, 100), "Arial", 12, 320, 360);
        }
    }
}
