using SplashKitSDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
        private bool            _showingItemMenu   = false;
        private int             _selectedItemIndex = 0;
        private bool            _showingSwapMenu   = false;
        private int             _selectedSwapIndex = 0;
        private bool            _showingShop       = false;
        private int             _selectedShopIndex = 0;
        private WildMonster?    _pendingEncounter        = null;
        private AITrainer?      _pendingTrainerEncounter = null;
        private AITrainer?      _lastTrainerBattled      = null;

        private readonly Random _rng = new Random();

        // Shop catalogue — name, gold cost, item factory
        private readonly (string Name, int Cost, Func<Item> Create)[] _shopCatalog =
        {
            ("Potion",       50,  () => new Potion("Potion",       30,   ItemRarity.Common)),
            ("Super Potion", 100, () => new Potion("Super Potion", 70,   ItemRarity.Rare)),
            ("Capture Orb",  80,  () => new CaptureOrb("Capture Orb", 1.0f, ItemRarity.Common)),
        };

        // Priority 4 — visual polish state
        private int             _battleAnimFrame     = 0;
        private float           _playerHpDisplay     = 1.0f;
        private float           _enemyHpDisplay      = 1.0f;
        private bool            _pendingBattleEnd    = false;
        private int             _battleResultTimer   = 0;
        private string          _battleResultMessage = "";

        // Sprite cache — loaded once in Run(), keyed by monster name
        private Dictionary<string, Bitmap> _monsterSprites = new();

        // Active battle animations — updated and drawn each frame, pruned when complete
        private List<BattleAnimation> _battleAnimations = new();

        // BGM — loaded once, swapped on state transitions
        private Music? _overworldMusic = null;
        private Music? _battleMusic    = null;
        private bool   _bgmReady       = false;
        private string _activeBgm      = "";

        // Inter-turn pacing — after player acts, enemy fires after _interTurnDelay frames
        private bool _pendingEnemyTurn = false;
        private int  _interTurnDelay   = 0;

        // Capture-end pacing — delays EndBattle after a successful catch so the animation plays
        private bool _pendingCaptureEnd = false;
        private int  _captureEndTimer   = 0;

        // Capture result screen data — populated when capture succeeds
        private string      _capturedMonsterName      = "";
        private int         _capturedMonsterLevel     = 1;
        private MonsterType _capturedMonsterPrimary   = MonsterType.Normal;
        private MonsterType _capturedMonsterSecondary = MonsterType.Normal;
        private bool        _capturedHasDual          = false;
        private int         _capturedHpAtCapture      = 0;
        private int         _capturedMaxHp            = 1;
        private string      _captureChanceLabel       = "";
        private string      _capturePartyMessage      = "";

        // Screen resolution constants — reference everywhere instead of hardcoded 800/600
        private const int ScreenW = 1280;
        private const int ScreenH = 720;

        private GameManager()
        {
            _window             = new Window("Monster Tamer: Guardians of Sarawak", ScreenW, ScreenH);
            _window.ToggleFullscreen();
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
            AITrainer t1 = new AITrainer("Ranger Lili", 1);
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
            LoadMonsterSprites();
            LoadBgm();

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
                if (_state == GameState.Battle)
                {
                    if (_pendingBattleEnd || _pendingCaptureEnd) return;
                    // Close sub-menus first; a clean Esc flees
                    if (_showingItemMenu || _showingSwapMenu)
                    {
                        _showingItemMenu = false;
                        _showingSwapMenu = false;
                    }
                    else
                    {
                        _battleManager?.Flee();
                        ShowBattleResult();
                    }
                }
                else if (_state == GameState.EncounterPrompt)
                {
                    _pendingEncounter        = null;
                    _pendingTrainerEncounter = null;
                    _state = GameState.Overworld;
                    _overworldMap?.ApplyFleeingCooldown();
                }
                else if (_state == GameState.PartyScreen)
                    _state = GameState.Overworld;
                else if (_state == GameState.CaptureScreen)
                    EndBattle();
                else if (_state == GameState.Overworld && _showingShop)
                    _showingShop = false;
                else if (_state == GameState.Overworld)
                {
                    StopBgm();
                    _state = GameState.MainMenu;
                }
                else
                    _window.Close();
                return;
            }

            switch (_state)
            {
                case GameState.MainMenu:
                    if (SplashKit.KeyTyped(KeyCode.ReturnKey) ||
                        SplashKit.KeyTyped(KeyCode.SpaceKey))
                    {
                        _state = GameState.Overworld;
                        PlayBgm("overworld");
                    }
                    break;

                case GameState.EncounterPrompt:
                    if (SplashKit.KeyTyped(KeyCode.ReturnKey))
                    {
                        if (_pendingEncounter != null)
                        {
                            StartWildBattle(_pendingEncounter);
                            _pendingEncounter = null;
                        }
                        else if (_pendingTrainerEncounter != null)
                        {
                            _lastTrainerBattled      = _pendingTrainerEncounter;
                            StartTrainerBattle(_pendingTrainerEncounter);
                            _pendingTrainerEncounter = null;
                        }
                    }
                    else if (SplashKit.KeyTyped(KeyCode.RKey))
                    {
                        _pendingEncounter        = null;
                        _pendingTrainerEncounter = null;
                        _state = GameState.Overworld;
                        _overworldMap?.ApplyFleeingCooldown();
                    }
                    break;

                case GameState.Battle:
                    HandleBattleInput();
                    break;

                case GameState.PartyScreen:
                    if (SplashKit.KeyTyped(KeyCode.PKey) ||
                        SplashKit.KeyTyped(KeyCode.ReturnKey))
                        _state = GameState.Overworld;
                    break;

                case GameState.CaptureScreen:
                    if (SplashKit.KeyTyped(KeyCode.ReturnKey))
                        EndBattle();
                    break;

                case GameState.Victory:
                case GameState.GameOver:
                    if (SplashKit.KeyTyped(KeyCode.ReturnKey))
                        _window.Close();
                    break;
            }

            if (_state == GameState.Overworld && _showingShop)
                HandleShopInput();
            else if (_state == GameState.Overworld &&
                     SplashKit.KeyTyped(KeyCode.PKey))
                _state = GameState.PartyScreen;
        }

        private void HandleBattleInput()
        {
            if (_battleManager == null) return;
            if (_pendingBattleEnd)
            {
                if (SplashKit.KeyTyped(KeyCode.ReturnKey))
                {
                    _pendingBattleEnd  = false;
                    _battleResultTimer = 0;
                    EndBattle();
                }
                return;
            }

            if (_pendingCaptureEnd) return;
            if (_pendingEnemyTurn)  return;

            if (_showingSwapMenu)
            {
                HandleSwapInput();
            }
            else if (_showingItemMenu)
            {
                HandleItemInput();
            }
            else
            {
                // Move selection
                if (SplashKit.KeyTyped(KeyCode.LeftKey))  _player.SelectedMoveIndex--;
                if (SplashKit.KeyTyped(KeyCode.RightKey)) _player.SelectedMoveIndex++;
                if (SplashKit.KeyTyped(KeyCode.UpKey))    _player.SelectedMoveIndex -= 2;
                if (SplashKit.KeyTyped(KeyCode.DownKey))  _player.SelectedMoveIndex += 2;

                if (SplashKit.KeyTyped(KeyCode.CKey))
                {
                    _showingItemMenu   = true;
                    _selectedItemIndex = 0;
                }
                if (SplashKit.KeyTyped(KeyCode.XKey))
                {
                    _showingSwapMenu   = true;
                    _selectedSwapIndex = 0;
                }

                if (SplashKit.KeyTyped(KeyCode.ReturnKey))
                {
                    _battleManager.PlayerTurn(_player.SelectedMoveIndex);
                    if (_battleManager.IsBattleOver())
                        ShowBattleResult();
                    else
                    {
                        _pendingEnemyTurn = true;
                        _interTurnDelay   = 150;
                    }
                }
            }
        }

        private void HandleItemInput()
        {
            if (_battleManager == null) return;

            // Wild battle: show all items; trainer/boss: hide Capture Orbs
            bool isWild = _battleManager.WildTarget != null;
            List<Item> items = _player.Bag.GetAllItems()
                .Where(i => isWild || !(i is CaptureOrb))
                .ToList();

            if (SplashKit.KeyTyped(KeyCode.UpKey)   || SplashKit.KeyTyped(KeyCode.LeftKey))
                _selectedItemIndex = Math.Max(0, _selectedItemIndex - 1);
            if (SplashKit.KeyTyped(KeyCode.DownKey) || SplashKit.KeyTyped(KeyCode.RightKey))
                _selectedItemIndex = Math.Min(Math.Max(0, items.Count - 1), _selectedItemIndex + 1);

            if (SplashKit.KeyTyped(KeyCode.CKey))
            {
                _showingItemMenu = false;
                return;
            }

            if (SplashKit.KeyTyped(KeyCode.ReturnKey) &&
                items.Count > 0 && _selectedItemIndex < items.Count)
            {
                Item item = items[_selectedItemIndex];
                _player.Bag.RemoveItem(item);
                _showingItemMenu = false;

                if (item is CaptureOrb orb)
                {
                    CaptureResult result = _battleManager.AttemptCapture(orb);
                    _battleAnimations.Add(new CaptureOrbAnimation(272f, 312f, 845f, 168f, result));

                    if (result == CaptureResult.Caught)
                    {
                        WildMonster wild = _battleManager.WildTarget!;
                        // Snapshot data for the capture result screen before taming
                        _capturedMonsterName      = wild.Name;
                        _capturedMonsterLevel     = wild.Level;
                        _capturedMonsterPrimary   = wild.PrimaryType;
                        _capturedMonsterSecondary = wild.SecondaryType;
                        _capturedHasDual          = wild.HasDualType;
                        _capturedHpAtCapture      = wild.Hp;
                        _capturedMaxHp            = wild.MaxHp;
                        float chance = orb.ComputeCatchChance(wild);
                        _captureChanceLabel = chance < 0.10f ? "Very Low"
                                            : chance < 0.20f ? "Low"
                                            : chance < 0.35f ? "Moderate"
                                            : chance < 0.50f ? "High"
                                            : "Very High";

                        TamedMonster tamed = MonsterFactory.Tame(wild);
                        bool added = _player.Party.AddMonster(tamed);
                        _capturePartyMessage = added
                            ? $"{tamed.Name} was added to your party!"
                            : $"{tamed.Name} was released - Party full";
                        _battleManager.AddLog(_capturePartyMessage, LogEntryType.Capture);
                        _pendingCaptureEnd = true;
                        _captureEndTimer   = 140;
                        return;
                    }
                }
                else
                {
                    // Potion or StatusCure — apply to active monster
                    TamedMonster? active = _player.GetActiveMonster();
                    if (active != null)
                    {
                        int hpBefore = active.Hp;
                        item.Use(active);
                        int healed = active.Hp - hpBefore;
                        _battleManager.AddTurnSeparatorIfNew();
                        _battleManager.AddLog($"+ Used {item.Name} on {active.Name}!", LogEntryType.Healing);

                        if (healed > 0)
                        {
                            _battleAnimations.Add(new PotionGlowAnimation(272f, 312f));
                            _battleAnimations.Add(new HealNumberAnimation(272f, 282f, healed));
                        }
                    }
                }

                // Item used — defer enemy turn so animations play first
                if (_battleManager.IsBattleOver())
                    ShowBattleResult();
                else
                {
                    _pendingEnemyTurn = true;
                    _interTurnDelay   = 150;
                }
            }
        }

        private void HandleSwapInput()
        {
            if (_battleManager == null) return;

            int partyCount = _player.Party.Count;

            if (SplashKit.KeyTyped(KeyCode.UpKey)   || SplashKit.KeyTyped(KeyCode.LeftKey))
                _selectedSwapIndex = Math.Max(0, _selectedSwapIndex - 1);
            if (SplashKit.KeyTyped(KeyCode.DownKey) || SplashKit.KeyTyped(KeyCode.RightKey))
                _selectedSwapIndex = Math.Min(partyCount - 1, _selectedSwapIndex + 1);

            if (SplashKit.KeyTyped(KeyCode.XKey))
            {
                _showingSwapMenu = false;
                return;
            }

            if (SplashKit.KeyTyped(KeyCode.ReturnKey))
            {
                TamedMonster  selected = _player.Party[_selectedSwapIndex];
                TamedMonster? active   = _player.GetActiveMonster();

                if (selected != active && selected.IsAlive)
                {
                    _player.Party.SwapToFront(_selectedSwapIndex);
                    _playerHpDisplay = _player.GetActiveMonster()?.HpPercent ?? 1.0f;
                    _battleManager.AddTurnSeparatorIfNew();
                    _battleManager.AddLog($"* Go, {selected.Name}!");
                    // Swapping uses the player's turn — defer enemy turn
                    if (_battleManager.IsBattleOver())
                        ShowBattleResult();
                    else
                    {
                        _pendingEnemyTurn = true;
                        _interTurnDelay   = 150;
                    }
                }
                _showingSwapMenu = false;
            }
        }

        private void HandleShopInput()
        {
            if (SplashKit.KeyTyped(KeyCode.UpKey)   || SplashKit.KeyTyped(KeyCode.WKey))
                _selectedShopIndex = Math.Max(0, _selectedShopIndex - 1);
            if (SplashKit.KeyTyped(KeyCode.DownKey) || SplashKit.KeyTyped(KeyCode.SKey))
                _selectedShopIndex = Math.Min(_shopCatalog.Length - 1, _selectedShopIndex + 1);

            if (SplashKit.KeyTyped(KeyCode.ReturnKey))
            {
                var (_, cost, create) = _shopCatalog[_selectedShopIndex];
                if (_player.Gold >= cost)
                {
                    Item newItem = create();
                    if (_player.Bag.AddItem(newItem))
                        _player.SpendGold(cost);
                }
            }
        }

        public void Update()
        {
            if (_state == GameState.Battle && _battleManager != null)
            {
                if (_battleAnimFrame < 30) _battleAnimFrame++;

                // Update animations; prune completed ones (iterate in reverse for safe removal)
                for (int i = _battleAnimations.Count - 1; i >= 0; i--)
                {
                    if (_battleAnimations[i].IsComplete)
                        _battleAnimations.RemoveAt(i);
                    else
                        _battleAnimations[i].Update();
                }

                // Inter-turn pacing: count down then fire enemy turn
                if (_pendingEnemyTurn)
                {
                    if (_interTurnDelay > 0)
                        _interTurnDelay--;
                    else
                    {
                        _pendingEnemyTurn = false;
                        _battleManager.EnemyTurn();
                        if (_battleManager.IsBattleOver(isEndOfRound: true))
                            ShowBattleResult();
                    }
                }

                TamedMonster? pm = _player.GetActiveMonster();
                Monster?      em = _battleManager.EnemyMonster;
                if (pm != null)
                {
                    float tgt = pm.HpPercent;
                    _playerHpDisplay += (tgt - _playerHpDisplay) * 0.12f;
                    if (Math.Abs(_playerHpDisplay - tgt) < 0.004f) _playerHpDisplay = tgt;
                }
                if (em != null)
                {
                    float tgt = em.HpPercent;
                    _enemyHpDisplay += (tgt - _enemyHpDisplay) * 0.12f;
                    if (Math.Abs(_enemyHpDisplay - tgt) < 0.004f) _enemyHpDisplay = tgt;
                }

                if (_pendingBattleEnd)
                {
                    _battleResultTimer--;
                    if (_battleResultTimer <= 0)
                    {
                        _pendingBattleEnd = false;
                        EndBattle();
                    }
                }

                if (_pendingCaptureEnd)
                {
                    _captureEndTimer--;
                    if (_captureEndTimer <= 0)
                    {
                        _pendingCaptureEnd = false;
                        _state = GameState.CaptureScreen;
                    }
                }
            }

            if (_state == GameState.Overworld && _overworldMap != null && !_showingShop)
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
                    if (encounter != null)
                    {
                        _pendingEncounter = encounter;
                        _state = GameState.EncounterPrompt;
                    }
                }
                else if (_overworldMap.TrainerEncounterTriggered &&
                         _overworldMap.TriggeredTrainer != null)
                {
                    _pendingTrainerEncounter = _overworldMap.TriggeredTrainer;
                    _state = GameState.EncounterPrompt;
                }
                else if (_overworldMap.RegionExitTriggered)
                {
                    AdvanceRegion();
                }
                else if (_overworldMap.ShopEncounterTriggered)
                {
                    _showingShop       = true;
                    _selectedShopIndex = 0;
                }
            }
        }

        public void Draw()
        {
            SplashKit.ClearScreen(SplashKit.RGBColor(15, 25, 12));

            switch (_state)
            {
                case GameState.MainMenu:        DrawMainMenu();                        break;
                case GameState.Overworld:       DrawOverworld(); if (_showingShop) DrawShop(); break;
                case GameState.EncounterPrompt: DrawOverworld(); DrawEncounterPrompt(); break;
                case GameState.Battle:          DrawBattle();                          break;
                case GameState.PartyScreen:     DrawPartyScreen();                     break;
                case GameState.CaptureScreen:   DrawCaptureScreen();                   break;
                case GameState.Victory:         DrawVictory();                         break;
                case GameState.GameOver:        DrawGameOver();                        break;
            }

            SplashKit.RefreshScreen(60);
        }

        private void StartWildBattle(WildMonster wild)
        {
            AITrainer dummy     = new AITrainer("Wild");
            _battleManager      = new BattleManager(_player, dummy);
            _battleManager.WildTarget      = wild;
            _battleManager.AttackHit      += OnAttackHit;
            _battleManager.HealUsed       += OnHealUsed;
            _battleManager.StatusApplied  += OnStatusApplied;
            _battleManager.TurnSkipped    += OnTurnSkipped;
            _battleManager.AddLog($"A wild {wild.Name} appeared!");
            _battleManager.AddLog($"{wild.GetBattleCry()}");
            _battleManager.AddLog($"Go, {_player.GetActiveMonster()?.Name}!");
            ResetBattleAnimState();
            PlayBgm("battle");
            _state = GameState.Battle;
        }

        private void StartTrainerBattle(AITrainer trainer)
        {
            _battleManager = new BattleManager(_player, trainer);
            _battleManager.AttackHit     += OnAttackHit;
            _battleManager.HealUsed      += OnHealUsed;
            _battleManager.StatusApplied += OnStatusApplied;
            _battleManager.TurnSkipped   += OnTurnSkipped;
            _battleManager.AddLog($"Trainer {trainer.Name} wants to battle!");
            _battleManager.AddLog($"Go, {_player.GetActiveMonster()?.Name}!");
            ResetBattleAnimState();
            PlayBgm("battle");
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
            BossMonster nagaBelian  = MonsterFactory.CreateNagaBelian();
            AITrainer   bossTrainer = new AITrainer("Naga Belian", 3);
            _battleManager = new BattleManager(_player, bossTrainer);
            _battleManager.BossTarget    = nagaBelian;
            _battleManager.AttackHit    += OnAttackHit;
            _battleManager.HealUsed     += OnHealUsed;
            _battleManager.StatusApplied+= OnStatusApplied;
            _battleManager.TurnSkipped  += OnTurnSkipped;
            _battleManager.AddLog("NAGA BELIAN EMERGES FROM THE DEPTHS!");
            _battleManager.AddLog(nagaBelian.GetBattleCry());
            ResetBattleAnimState();
            PlayBgm("battle");
            _state = GameState.Battle;
        }

        private void EndBattle()
        {
            if (_battleManager == null) return;
            _showingItemMenu   = false;
            _selectedItemIndex = 0;
            _showingSwapMenu   = false;
            _selectedSwapIndex = 0;
            if (_battleManager.Result == BattleResult.PlayerDied)
            {
                _lastTrainerBattled = null;
                StopBgm();
                _state = GameState.GameOver;
                return;
            }
            if (_battleManager.Result == BattleResult.PlayerWon)
            {
                // Gold reward: 50–100 per win
                _player.AddGold(50 + _rng.Next(51));

                // Mark the specific NPC defeated on the map only after a real win
                if (_lastTrainerBattled != null)
                    _overworldMap?.MarkTrainerDefeated(_lastTrainerBattled);

                // Check if all trainers defeated — clear region
                bool allDefeated = true;
                foreach (AITrainer t in _regions[_currentRegionIndex].Trainers)
                    if (!t.Party.IsWiped()) { allDefeated = false; break; }
                if (allDefeated)
                {
                    _regions[_currentRegionIndex].Clear();
                    _overworldMap?.SetCleared(true);
                }
            }
            _lastTrainerBattled = null;
            PlayBgm("overworld");
            _state = GameState.Overworld;
        }

        private void ResetBattleAnimState()
        {
            _battleAnimFrame     = 0;
            _playerHpDisplay     = _player.GetActiveMonster()?.HpPercent ?? 1.0f;
            _enemyHpDisplay      = 1.0f;
            _pendingBattleEnd    = false;
            _battleResultTimer   = 0;
            _battleResultMessage = "";
            _battleAnimations.Clear();
            _pendingEnemyTurn    = false;
            _interTurnDelay      = 0;
            _pendingCaptureEnd   = false;
            _captureEndTimer     = 0;
        }

        // Fired by BattleManager.AttackHit — creates a hit flash + floating damage number
        // at the target's sprite position.  Player monster is on the left (272, 312);
        // enemy is on the right (845, 168).  These match the DrawBattle() DrawMonsterSprite calls.
        private void OnAttackHit(Monster target, int damage)
        {
            bool  isEnemy = (target == _battleManager?.EnemyMonster);
            float cx      = isEnemy ? 845f : 272f;
            float cy      = isEnemy ? 168f : 312f;
            _battleAnimations.Add(new HitFlashAnimation(cx, cy));
            _battleAnimations.Add(new DamageNumberAnimation(cx, cy - 30f, damage));
        }

        private void OnHealUsed(Monster caster, int healAmount)
        {
            bool  isEnemy = (caster == _battleManager?.EnemyMonster);
            float cx      = isEnemy ? 845f : 272f;
            float cy      = isEnemy ? 168f : 312f;
            _battleAnimations.Add(new HealFlashAnimation(cx, cy));
            _battleAnimations.Add(new HealNumberAnimation(cx, cy - 30f, healAmount));
            _battleAnimations.Add(new HealParticleAnimation(cx, cy));
        }

        private void OnStatusApplied(Monster target, StatusCondition status)
        {
            bool  isEnemy = (target == _battleManager?.EnemyMonster);
            float cx      = isEnemy ? 845f : 272f;
            float cy      = isEnemy ? 168f : 312f;
            _battleAnimations.Add(new StatusSwirlAnimation(cx, cy, status));
        }

        private void OnTurnSkipped(Monster monster, StatusCondition status)
        {
            bool  isEnemy = monster == _battleManager?.EnemyMonster;
            float cx      = isEnemy ? 845f : 272f;
            float cy      = isEnemy ? 168f : 312f;
            _battleAnimations.Add(new TurnSkipAnimation(cx, cy, status));
        }

        private void ShowBattleResult()
        {
            if (_battleManager == null) return;
            string enemyName  = _battleManager.EnemyMonster?.Name ?? "Enemy";
            string playerName = !string.IsNullOrEmpty(_battleManager.LastPlayerMonsterName)
                                    ? _battleManager.LastPlayerMonsterName
                                    : "Your monster";
            _battleResultMessage = _battleManager.Result switch
            {
                BattleResult.PlayerWon  => _battleManager.BossTarget != null
                                            ? "You defeated Naga Belian!"
                                            : $"You won!  {enemyName} fainted!",
                BattleResult.PlayerDied => $"{playerName} fainted!  Defeated...",
                BattleResult.PlayerFled => "Got away safely!",
                _                       => "Battle over!"
            };
            _pendingBattleEnd  = true;
            _battleResultTimer = 150;
        }

        // ── Draw methods ────────────────────────────────────────

        private void DrawMainMenu()
        {
            int cx = ScreenW / 2; // horizontal centre = 640

            // Sky gradient — top half darker, bottom slightly lighter
            SplashKit.FillRectangle(SplashKit.RGBColor(10, 40, 20),  0,            0, ScreenW, ScreenH / 2);
            SplashKit.FillRectangle(SplashKit.RGBColor(15, 55, 25),  0, ScreenH / 2, ScreenW, ScreenH / 2);

            // Jungle silhouette spread across full 1280px
            Color jungle = SplashKit.RGBColor(8, 30, 8);
            SplashKit.FillEllipse(jungle,  -20, 320, 280, 170);
            SplashKit.FillEllipse(jungle,  180, 292, 340, 190);
            SplashKit.FillEllipse(jungle,  490, 308, 310, 175);
            SplashKit.FillEllipse(jungle,  760, 285, 395, 205);
            SplashKit.FillEllipse(jungle, 1050, 300, 315, 180);
            SplashKit.FillRectangle(jungle, 0, 398, ScreenW, ScreenH - 398);

            // Twinkling stars across full sky
            long fireT = (long)SplashKit.TimerTicks("blink");
            for (int i = 0; i < 30; i++)
            {
                double drift   = fireT / 2000.0 + i * 0.65;
                int    fx      = (int)(80 + (i * 191 + 40) % (ScreenW - 160)
                                     + Math.Cos(drift) * 18);
                int    fy      = (int)(15 + (i * 67  + 20) % (ScreenH / 2 - 80)
                                     + Math.Sin(drift * 1.3) * 10);
                double twinkle = 0.35 + 0.65 * Math.Abs(Math.Sin(fireT / 420.0 + i * 1.1));
                int    alpha   = (int)(twinkle * 230);
                float  radius  = (float)(0.8 + Math.Abs(Math.Sin(fireT / 650.0 + i * 0.9)));
                SplashKit.FillCircle(SplashKit.RGBAColor(255, 255, 160, alpha), fx, fy, radius);
                if (twinkle > 0.75)
                    SplashKit.FillCircle(SplashKit.RGBAColor(255, 255, 100, (int)(twinkle * 55)),
                        fx, fy, radius * 2.5f);
            }
            // Fireflies drifting near the silhouette line
            for (int i = 0; i < 20; i++)
            {
                double drift2   = fireT / 2400.0 + i * 1.05;
                int    fx2      = (int)(20 + (i * 157 + 70) % (ScreenW - 40)
                                      + Math.Cos(drift2 * 0.8) * 28);
                int    fy2      = (int)(350 + (i * 43  + 10) % 100
                                      + Math.Sin(drift2) * 15);
                double twinkle2 = 0.25 + 0.75 * Math.Abs(Math.Sin(fireT / 370.0 + i * 1.4));
                int    alpha2   = (int)(twinkle2 * 210);
                SplashKit.FillCircle(SplashKit.RGBAColor(160, 255, 130, alpha2), fx2, fy2, 1.8f);
            }

            // ── Title card panel (centred, 700px wide) ──────────────────────────
            int panelX = cx - 350; // 290
            int panelY = 128;
            SplashKit.FillRectangle(SplashKit.RGBAColor(0, 15, 0, 175), panelX, panelY, 700, 200);
            SplashKit.DrawRectangle(SplashKit.RGBColor(80, 180, 80),     panelX, panelY, 700, 200);
            SplashKit.DrawRectangle(SplashKit.RGBColor(45, 106, 79),     panelX + 6, panelY + 6, 688, 188);

            // Title text — centred by measured width
            int t1w = SplashKit.TextWidth("Monster Tamer", "Arial", 52);
            SplashKit.DrawText("Monster Tamer",
                SplashKit.RGBColor(255, 215, 0), "Arial", 52, cx - t1w / 2, panelY + 18);
            int t2w = SplashKit.TextWidth("Guardians of Sarawak", "Arial", 26);
            SplashKit.DrawText("Guardians of Sarawak",
                SplashKit.RGBColor(116, 198, 157), "Arial", 26, cx - t2w / 2, panelY + 88);

            // Decorative divider inside panel
            SplashKit.DrawLine(SplashKit.RGBColor(45, 106, 79),
                panelX + 20, panelY + 124, panelX + 680, panelY + 124);

            // Tagline — centred
            string tagline = "Explore Sarawak. Tame the Wild. Become a Guardian.";
            int tagW = SplashKit.TextWidth(tagline, "Arial", 12);
            SplashKit.DrawText(tagline,
                SplashKit.RGBColor(100, 160, 110), "Arial", 12, cx - tagW / 2, panelY + 136);

            // ── Monster roster ───────────────────────────────────────────────────
            SplashKit.DrawText("[ Sarawak Monster Roster ]",
                SplashKit.RGBColor(80, 160, 110), "Arial", 12, cx - 96, 356);

            string[] names = { "Kelabit", "Bekantan", "Mawas",  "Rajah",
                                "Enggang", "Pelanduk", "Temaju", "Buaya" };
            // 4-column grid, centred (each slot 200px wide → total 800px, offset = (1280-800)/2 = 240)
            for (int i = 0; i < names.Length; i++)
            {
                int col = i % 4, row = i / 4;
                int tx  = 240 + col * 200;
                int ty  = 378 + row * 24;
                SplashKit.DrawText("*  " + names[i],
                    SplashKit.RGBColor(170, 220, 170), "Arial", 13, tx, ty);
            }
            // Final boss line — centred, ASCII only
            string bossLine = "*  Naga Belian  -  Final Boss  -  Legendary";
            int bossLineW = SplashKit.TextWidth(bossLine, "Arial", 13);
            SplashKit.DrawText(bossLine,
                SplashKit.RGBColor(255, 215, 0), "Arial", 13, cx - bossLineW / 2, 430);

            // ── Blinking start prompt ────────────────────────────────────────────
            bool blink = (SplashKit.TimerTicks("blink") % 900) < 600;
            if (blink)
            {
                SplashKit.FillRectangle(SplashKit.RGBColor(26, 71, 49), cx - 155, 458, 310, 40);
                SplashKit.DrawRectangle(SplashKit.RGBColor(80, 180, 80), cx - 155, 458, 310, 40);
                string startText = "Press  Enter  to  Start";
                int stW = SplashKit.TextWidth(startText, "Arial", 15);
                SplashKit.DrawText(startText,
                    SplashKit.RGBColor(116, 198, 157), "Arial", 15, cx - stW / 2, 469);
            }

            // ── Flavour info line ────────────────────────────────────────────────
            SplashKit.DrawText("3 Regions  -  8 Tameables  -  1 Legendary Boss",
                SplashKit.RGBColor(70, 150, 90), "Arial", 11, cx - 192, 516);

            // ── Bottom hints bar ─────────────────────────────────────────────────
            SplashKit.FillRectangle(SplashKit.RGBAColor(0, 0, 0, 110), 0, ScreenH - 38, ScreenW, 38);
            SplashKit.DrawText("Enter: Start          Esc: Quit          P: Party Screen",
                SplashKit.RGBColor(100, 160, 110), "Arial", 11, cx - 215, ScreenH - 22);

            // Corner credit
            SplashKit.DrawText("Option 4  ·  C# with SplashKit",
                SplashKit.RGBColor(45, 106, 79), "Arial", 10, ScreenW - 284, ScreenH - 22);
        }

        private void DrawOverworld()
        {
            if (_overworldMap != null)
                _overworldMap.Draw(_window, _monsterSprites);

            // Gold badge — top-right, above the map HUD
            string goldText = $"Gold: {_player.Gold}G";
            int    gtw      = SplashKit.TextWidth(goldText, "Arial", 11);
            int    badgeX   = ScreenW - gtw - 30;
            SplashKit.FillRectangle(SplashKit.RGBAColor(10, 40, 10, 200), badgeX - 10, 8, gtw + 20, 26);
            SplashKit.DrawRectangle(SplashKit.RGBColor(255, 215, 0),       badgeX - 10, 8, gtw + 20, 26);
            SplashKit.DrawText(goldText, SplashKit.RGBColor(255, 215, 0), "Arial", 11, badgeX, 15);
        }

        private void DrawShop()
        {
            int cx    = ScreenW / 2;
            int cardW = 560, cardH = 380;
            int cardX = cx - cardW / 2, cardY = 170;

            // Dim background
            SplashKit.FillRectangle(SplashKit.RGBAColor(0, 0, 0, 170), 0, 0, ScreenW, ScreenH);

            // Card
            SplashKit.FillRectangle(SplashKit.RGBAColor(30, 20, 5, 245), cardX, cardY, cardW, cardH);
            SplashKit.DrawRectangle(SplashKit.RGBColor(255, 215, 0),      cardX, cardY, cardW, cardH);
            SplashKit.DrawRectangle(SplashKit.RGBColor(180, 130, 30),     cardX + 3, cardY + 3, cardW - 6, cardH - 6);

            // Header strip
            SplashKit.FillRectangle(SplashKit.RGBAColor(180, 110, 10, 200), cardX + 1, cardY + 1, cardW - 2, 36);
            string title = "SHOP";
            int    titleW = SplashKit.TextWidth(title, "Arial", 18);
            SplashKit.DrawText(title, SplashKit.RGBColor(255, 215, 0), "Arial", 18, cx - titleW / 2, cardY + 9);

            // Player gold
            string goldText = $"Gold: {_player.Gold}G";
            int    goldTw   = SplashKit.TextWidth(goldText, "Arial", 13);
            SplashKit.DrawText(goldText, SplashKit.RGBColor(255, 215, 0), "Arial", 13, cx - goldTw / 2, cardY + 48);

            // Item rows
            int listY = cardY + 76;
            for (int i = 0; i < _shopCatalog.Length; i++)
            {
                var (name, cost, _) = _shopCatalog[i];
                bool sel       = (i == _selectedShopIndex);
                bool canAfford = (_player.Gold >= cost);
                int  itemX     = cardX + 20;
                int  itemW     = cardW - 40;
                int  itemY     = listY + i * 82;

                SplashKit.FillRectangle(
                    sel ? SplashKit.RGBColor(80, 60, 10) : SplashKit.RGBColor(40, 30, 5),
                    itemX, itemY, itemW, 70);
                SplashKit.DrawRectangle(
                    sel ? SplashKit.RGBColor(255, 215, 0) : SplashKit.RGBColor(140, 100, 20),
                    itemX, itemY, itemW, 70);

                Color nameC = canAfford ? SplashKit.ColorWhite() : SplashKit.RGBColor(140, 120, 80);
                SplashKit.DrawText(name, nameC, "Arial", 15, itemX + 12, itemY + 10);

                string costStr = $"{cost}G";
                Color  costC   = canAfford
                    ? SplashKit.RGBColor(255, 215, 0)
                    : SplashKit.RGBColor(180, 80, 80);
                SplashKit.DrawText(costStr, costC, "Arial", 13, itemX + 12, itemY + 38);

                if (!canAfford)
                    SplashKit.DrawText("Not enough gold",
                        SplashKit.RGBColor(180, 80, 80), "Arial", 10, itemX + 90, itemY + 40);

                if (sel)
                    SplashKit.DrawText(">",
                        SplashKit.RGBColor(255, 215, 0), "Arial", 15, cardX + 6, itemY + 10);
            }

            // Controls hint
            SplashKit.DrawLine(SplashKit.RGBColor(100, 70, 10),
                cardX + 20, cardY + cardH - 36, cardX + cardW - 20, cardY + cardH - 36);
            string hint = "Up/Down: Select     Enter: Buy     Esc: Leave";
            int    hintW = SplashKit.TextWidth(hint, "Arial", 11);
            SplashKit.DrawText(hint, SplashKit.RGBColor(160, 140, 80), "Arial", 11,
                cx - hintW / 2, cardY + cardH - 24);
        }

        private void DrawEncounterPrompt()
        {
            if (_pendingEncounter != null)
                DrawWildEncounterCard();
            else if (_pendingTrainerEncounter != null)
                DrawTrainerEncounterCard();
        }

        private void DrawEncounterCardShell(int cardX, int cardY, int cardW, int cardH, string title)
        {
            int cardCx = cardX + cardW / 2;
            SplashKit.FillRectangle(SplashKit.RGBAColor(0, 0, 0, 170), 0, 0, ScreenW, ScreenH);
            SplashKit.FillRectangle(SplashKit.RGBAColor(10, 28, 12, 245), cardX, cardY, cardW, cardH);
            SplashKit.DrawRectangle(SplashKit.RGBColor(80, 200, 80),       cardX, cardY, cardW, cardH);
            SplashKit.DrawRectangle(SplashKit.RGBColor(45, 120, 45),       cardX + 3, cardY + 3, cardW - 6, cardH - 6);
            SplashKit.FillRectangle(SplashKit.RGBAColor(30, 100, 30, 200), cardX + 1, cardY + 1, cardW - 2, 38);
            int titleW = SplashKit.TextWidth(title, "Arial", 18);
            SplashKit.DrawText(title, SplashKit.RGBColor(255, 215, 0), "Arial", 18,
                cardCx - titleW / 2, cardY + 10);
        }

        private void DrawEncounterButtonRow(int cardX, int btnY, int cardW)
        {
            int cardCx = cardX + cardW / 2;
            int btnW   = 240;
            int btnH   = 88;
            int fightX = cardCx - 20 - btnW;
            int runX   = cardCx + 20;

            SplashKit.DrawLine(SplashKit.RGBColor(45, 100, 45),
                cardX + 20, btnY - 8, cardX + cardW - 20, btnY - 8);

            SplashKit.FillRectangle(SplashKit.RGBColor(30, 90, 30),   fightX, btnY, btnW, btnH);
            SplashKit.DrawRectangle(SplashKit.RGBColor(255, 215,   0), fightX, btnY, btnW, btnH);
            string fl  = "FIGHT";
            int    flW = SplashKit.TextWidth(fl, "Arial", 18);
            SplashKit.DrawText(fl, SplashKit.RGBColor(255, 215, 0), "Arial", 18,
                fightX + btnW / 2 - flW / 2, btnY + 14);
            string fk  = "[Enter]";
            int    fkW = SplashKit.TextWidth(fk, "Arial", 12);
            SplashKit.DrawText(fk, SplashKit.RGBColor(170, 220, 170), "Arial", 12,
                fightX + btnW / 2 - fkW / 2, btnY + 44);

            SplashKit.FillRectangle(SplashKit.RGBColor(60, 30, 10),   runX, btnY, btnW, btnH);
            SplashKit.DrawRectangle(SplashKit.RGBColor(200, 120, 50),  runX, btnY, btnW, btnH);
            string rl  = "RUN";
            int    rlW = SplashKit.TextWidth(rl, "Arial", 18);
            SplashKit.DrawText(rl, SplashKit.RGBColor(230, 160, 80), "Arial", 18,
                runX + btnW / 2 - rlW / 2, btnY + 14);
            string rk  = "[Esc / R]";
            int    rkW = SplashKit.TextWidth(rk, "Arial", 12);
            SplashKit.DrawText(rk, SplashKit.RGBColor(180, 140, 100), "Arial", 12,
                runX + btnW / 2 - rkW / 2, btnY + 44);
        }

        private void DrawWildEncounterCard()
        {
            WildMonster m = _pendingEncounter!;

            int cardX = 360, cardY = 140, cardW = 560, cardH = 420;
            int cardCx = cardX + cardW / 2; // 640

            DrawEncounterCardShell(cardX, cardY, cardW, cardH, "! WILD ENCOUNTER !");

            // Sprite in upper section — baseSize=90f keeps it clear of text below
            // displaySize = 90 * 1.3 = 117px, half ≈ 59px
            int spriteCy = cardY + 118;
            DrawMonsterSprite(m.Name, cardCx, spriteCy, false, 90f);

            // Text info starts below sprite bottom (spriteCy + 59 + gap)
            int infoY = spriteCy + 72;

            string nameText = m.Name;
            int nameW = SplashKit.TextWidth(nameText, "Arial", 20);
            SplashKit.DrawText(nameText, SplashKit.ColorWhite(), "Arial", 20,
                cardCx - nameW / 2, infoY);

            string lvText = $"Lv.{m.Level}";
            int lvW = SplashKit.TextWidth(lvText, "Arial", 14);
            SplashKit.DrawText(lvText, SplashKit.RGBColor(170, 220, 170), "Arial", 14,
                cardCx - lvW / 2, infoY + 26);

            int primaryW    = TypeBadgeWidth(m.PrimaryType);
            int secondaryW  = m.HasDualType ? TypeBadgeWidth(m.SecondaryType) : 0;
            int totalBadgeW = primaryW + (m.HasDualType ? secondaryW + 6 : 0);
            int badgeStartX = cardCx - totalBadgeW / 2;
            DrawTypeBadge(m.PrimaryType, badgeStartX, infoY + 48);
            if (m.HasDualType)
                DrawTypeBadge(m.SecondaryType, badgeStartX + primaryW + 6, infoY + 48);

            DrawEncounterButtonRow(cardX, infoY + 72, cardW);
        }

        private void DrawTrainerEncounterCard()
        {
            AITrainer trainer = _pendingTrainerEncounter!;

            int cardX  = 200, cardY = 90, cardW = 880, cardH = 460;
            int cardCx = cardX + cardW / 2; // 640

            DrawEncounterCardShell(cardX, cardY, cardW, cardH, "! TRAINER ENCOUNTER !");

            // Trainer name
            string trName = trainer.Name;
            int    tnW    = SplashKit.TextWidth(trName, "Arial", 20);
            SplashKit.DrawText(trName, SplashKit.ColorWhite(), "Arial", 20,
                cardCx - tnW / 2, cardY + 48);

            // Inner divider + party label
            SplashKit.DrawLine(SplashKit.RGBColor(45, 120, 45),
                cardX + 20, cardY + 76, cardX + cardW - 20, cardY + 76);
            SplashKit.DrawText("Party:", SplashKit.RGBColor(130, 190, 130), "Arial", 11,
                cardX + 20, cardY + 82);

            // Monster slots — up to 3 per row
            // baseSize=90f, scale=1.3 → displaySize≈117px, half≈59px
            int   monCount  = Math.Min(trainer.Party.Count, 6);
            int   perRow    = Math.Min(monCount, 3);
            int   slotW     = cardW / (perRow > 0 ? perRow : 1);
            int   spriteCy  = cardY + 158;
            int   rowGap    = 165;
            const int SpriteHalf = 59;

            for (int i = 0; i < monCount; i++)
            {
                TamedMonster m      = trainer.Party[i];
                int          col    = i % perRow;
                int          row    = i / perRow;
                float        monCx  = cardX + slotW * col + slotW / 2.0f;
                int          monCy  = spriteCy + row * rowGap;

                DrawMonsterSprite(m.Name, monCx, monCy, false, 90f);

                // Text below sprite bottom — no overlap
                int textY = monCy + SpriteHalf + 8;

                string nameText = m.Name;
                int    nw       = SplashKit.TextWidth(nameText, "Arial", 14);
                SplashKit.DrawText(nameText, SplashKit.ColorWhite(), "Arial", 14,
                    (int)(monCx - nw / 2), textY);

                string lvText = $"Lv.{m.Level}";
                int    lw     = SplashKit.TextWidth(lvText, "Arial", 11);
                SplashKit.DrawText(lvText, SplashKit.RGBColor(170, 220, 170), "Arial", 11,
                    (int)(monCx - lw / 2), textY + 18);

                int primaryW  = TypeBadgeWidth(m.PrimaryType);
                int secondaryW = m.HasDualType ? TypeBadgeWidth(m.SecondaryType) : 0;
                int totalBW   = primaryW + (m.HasDualType ? secondaryW + 6 : 0);
                int bStartX   = (int)(monCx - totalBW / 2);
                DrawTypeBadge(m.PrimaryType, bStartX, textY + 34);
                if (m.HasDualType)
                    DrawTypeBadge(m.SecondaryType, bStartX + primaryW + 6, textY + 34);
            }

            DrawEncounterButtonRow(cardX, cardY + 370, cardW);
        }

        private void DrawBattle()
        {
            if (_battleManager == null) return;

            TamedMonster? playerMon = _player.GetActiveMonster();
            Monster? enemyMon = _battleManager.EnemyMonster;

            // Region-specific background palette
            bool  isBoss = _battleManager?.BossTarget != null;
            Color skyCol, groundCol, silhouetteCol, cloudCol, platFill, platEdge;
            if (isBoss)
            {
                skyCol        = SplashKit.RGBColor(  5,   5,  30);
                groundCol     = SplashKit.RGBColor( 10,  20,  55);
                silhouetteCol = SplashKit.RGBColor(  0,   8,  40);
                cloudCol      = SplashKit.RGBAColor(30,  55, 140, 180);
                platFill      = SplashKit.RGBColor(  0,  25,  75);
                platEdge      = SplashKit.RGBColor(  0,  55, 130);
            }
            else if (_currentRegionIndex == 1)   // River Delta — misty morning
            {
                skyCol        = SplashKit.RGBColor(140, 195, 215);
                groundCol     = SplashKit.RGBColor( 80, 108,  55);
                silhouetteCol = SplashKit.RGBColor( 35,  72,  30);
                cloudCol      = SplashKit.RGBColor(225, 235, 245);
                platFill      = SplashKit.RGBColor( 55,  88,  36);
                platEdge      = SplashKit.RGBColor( 70, 110,  45);
            }
            else if (_currentRegionIndex == 2)   // Ancient Highlands — dusk
            {
                skyCol        = SplashKit.RGBColor( 75,  65, 105);
                groundCol     = SplashKit.RGBColor(100,  88,  72);
                silhouetteCol = SplashKit.RGBColor( 45,  40,  35);
                cloudCol      = SplashKit.RGBColor(180, 165, 200);
                platFill      = SplashKit.RGBColor( 80,  65,  50);
                platEdge      = SplashKit.RGBColor( 98,  82,  62);
            }
            else                                  // Rainforest (region 0)
            {
                skyCol        = SplashKit.RGBColor(100, 180, 240);
                groundCol     = SplashKit.RGBColor( 60, 140,  60);
                silhouetteCol = SplashKit.RGBColor( 25,  80,  15);
                cloudCol      = SplashKit.RGBColor(230, 240, 255);
                platFill      = SplashKit.RGBColor( 40, 100,  25);
                platEdge      = SplashKit.RGBColor( 60, 130,  40);
            }

            SplashKit.FillRectangle(skyCol,    0,   0, ScreenW, ScreenH - 340);
            SplashKit.FillRectangle(groundCol, 0, 310, ScreenW, ScreenH - 310);

            // Clouds / atmosphere spread across full 1280px
            SplashKit.FillEllipse(cloudCol,   80,  40, 130, 42);
            SplashKit.FillEllipse(cloudCol,  110,  28,  90, 32);
            SplashKit.FillEllipse(cloudCol,  420,  50, 140, 44);
            SplashKit.FillEllipse(cloudCol,  455,  36,  95, 30);
            SplashKit.FillEllipse(cloudCol,  800,  42, 130, 40);
            SplashKit.FillEllipse(cloudCol,  832,  30,  85, 28);
            SplashKit.FillEllipse(cloudCol, 1090,  46, 150, 45);
            SplashKit.FillEllipse(cloudCol, 1125,  33, 100, 30);

            // Background silhouette spread across 1280px
            SplashKit.FillEllipse(silhouetteCol,   10, 215, 200, 120);
            SplashKit.FillEllipse(silhouetteCol,  170, 198, 250, 130);
            SplashKit.FillEllipse(silhouetteCol,  490, 210, 240, 120);
            SplashKit.FillEllipse(silhouetteCol,  720, 198, 280, 130);
            SplashKit.FillEllipse(silhouetteCol,  970, 212, 220, 118);
            SplashKit.FillEllipse(silhouetteCol, 1100, 196, 260, 130);
            SplashKit.FillRectangle(silhouetteCol, 0, 268, ScreenW, 80);

            // Enemy platform (right side of screen)
            SplashKit.FillEllipse(platFill, 708, 198, 270, 50);
            SplashKit.DrawEllipse(platEdge, 708, 198, 270, 50);

            // Player platform (left side of screen)
            SplashKit.FillEllipse(platFill, 132, 330, 280, 54);
            SplashKit.DrawEllipse(platEdge, 132, 330, 280, 54);

            // Entrance slide-in animation (eases over first 25 frames)
            float slideT    = Math.Min(1f, _battleAnimFrame / 25f);
            float easeOut   = 1f - (1f - slideT) * (1f - slideT);
            float enemyOffX = (1f - easeOut) * 400f;
            float plyrOffX  = -(1f - easeOut) * 400f;

            // Draw monster sprites
            if (enemyMon != null)
                DrawMonsterSprite(enemyMon.Name, 845 + enemyOffX, 168, false);
            if (playerMon != null)
                DrawMonsterSprite(playerMon.Name, 272 + plyrOffX, 312, true);

            // Draw active battle animations (hit flash, damage numbers) above sprites, below UI
            foreach (BattleAnimation anim in _battleAnimations)
                anim.Draw();

            // Battle log first so info panels render on top of it
            DrawBattleLog();

            // Dedicated tips strip above the panels (y=430–452) — always visible in battle
            if (playerMon != null && enemyMon != null)
                DrawBattleTips(playerMon, enemyMon);

            // Move / item / swap panel (y=452+)
            if (playerMon != null)
            {
                if (_showingSwapMenu)
                    DrawSwapPanel();
                else if (_showingItemMenu)
                    DrawItemPanel();
                else
                    DrawMovePanel(playerMon, enemyMon);
            }

            // Enemy info panel — top left (drawn after log so it's on top)
            if (enemyMon != null)
                DrawInfoPanel(enemyMon, 16, 16, 360, 110, false, _enemyHpDisplay);
            if (enemyMon is BossMonster bossDisplay)
                SplashKit.DrawText($"- PHASE {bossDisplay.Phase} / {bossDisplay.TotalPhases} -",
                    SplashKit.RGBColor(255, 80, 80), "Arial", 11, 16, 130);

            // Player info panel — lower-right scene area, above the battle log (y:430)
            if (playerMon != null)
                DrawInfoPanel(playerMon, ScreenW - 380, 318, 364, 108, true, _playerHpDisplay);

            // Controls hint — context-sensitive, inside left panel bottom
            string hint = _showingSwapMenu
                ? "Up/Down: Select Monster     Enter: Send Out     X: Back"
                : _showingItemMenu
                    ? "Up/Down: Select Item     Enter: Use     C: Back"
                    : "Arrows/Enter: Use Move     C: Bag     X: Swap     Esc: Flee";
            SplashKit.DrawText(hint, SplashKit.RGBColor(90, 110, 90), "Arial", 10, 22, 694);

            // Battle result overlay — fades in over 25 frames, then holds
            if (_pendingBattleEnd && _battleManager != null)
            {
                float fadeFrac = Math.Min(1f, (150 - _battleResultTimer) / 25f);
                int   overlayA = (int)(fadeFrac * 185);
                SplashKit.FillRectangle(SplashKit.RGBAColor(0, 0, 0, overlayA), 0, 0, ScreenW, ScreenH);
                if (fadeFrac > 0.25f)
                {
                    Color resultColor = _battleManager.Result == BattleResult.PlayerWon
                        ? SplashKit.RGBColor(255, 215,   0)
                        : _battleManager.Result == BattleResult.PlayerDied
                            ? SplashKit.RGBColor(220,  60,  60)
                            : SplashKit.RGBColor(140, 200, 255);
                    int msgWidth = SplashKit.TextWidth(_battleResultMessage, "Arial", 28);
                    int msgX = ScreenW / 2 - msgWidth / 2;
                    SplashKit.DrawText(_battleResultMessage, resultColor, "Arial", 28, msgX, ScreenH / 2 - 50);
                    int skipA = (int)(Math.Min(1f, (150 - _battleResultTimer) / 60f) * 210);
                    SplashKit.DrawText("Press Enter to continue",
                        SplashKit.RGBAColor(200, 200, 200, skipA), "Arial", 12, ScreenW / 2 - 90, ScreenH / 2 + 10);
                }
            }
        }

        private void LoadMonsterSprites()
        {
            var nameToFile = new Dictionary<string, string>
            {
                ["Kelabit"]     = "kelabit",
                ["Bekantan"]    = "bekantan",
                ["Mawas"]       = "mawas",
                ["Rajah"]       = "rajah",
                ["Enggang"]     = "enggang",
                ["Pelanduk"]    = "pelanduk",
                ["Temaju"]      = "temaju",
                ["Buaya"]       = "buaya",
                ["Naga Belian"] = "nagabelian",
            };

            foreach (var (monsterName, filename) in nameToFile)
                _monsterSprites[monsterName] = SplashKit.LoadBitmap(filename, filename + ".png");

            var overworldFiles = new Dictionary<string, string>
            {
                ["trainer"]        = "trainer",
                ["trainer_back"]   = "trainer_back",
                ["trainer_left"]   = "trainer_left",
                ["trainer_right"]  = "trainer_right",
                ["ranger_lili"]    = "ranger_lili",
                ["ranger_azman"]   = "ranger_azman",
                ["champion_dayak"] = "champion_dayak",
            };

            foreach (var (key, filename) in overworldFiles)
                _monsterSprites[key] = SplashKit.LoadBitmap(filename, filename + ".png");
        }

        private void LoadBgm()
        {
            string audioDir = Path.Combine(Directory.GetCurrentDirectory(), "resources", "audio");
            _overworldMusic = SplashKit.LoadMusic("overworld", Path.Combine(audioDir, "overworld.ogg"));
            _battleMusic    = SplashKit.LoadMusic("battle",    Path.Combine(audioDir, "battle.ogg"));
            _bgmReady = true;
        }

        private void PlayBgm(string name)
        {
            if (!_bgmReady || _activeBgm == name) return;
            SplashKit.StopMusic();
            _activeBgm = name;
            if (name == "overworld" && _overworldMusic != null)
                SplashKit.PlayMusic(_overworldMusic, -1);
            else if (name == "battle" && _battleMusic != null)
                SplashKit.PlayMusic(_battleMusic, -1);
        }

        private void StopBgm()
        {
            SplashKit.StopMusic();
            _activeBgm = "";
        }

        private void DrawMonsterSprite(string name, float cx, float cy, bool isPlayer, float baseSize = 180f)
        {
            float  scale       = isPlayer ? 1.0f : 1.3f;
            float  displaySize = baseSize * scale;
            double bmpScale    = displaySize / 1024.0;

            // Shadow stays grounded — not affected by the bob below
            SplashKit.FillEllipse(SplashKit.RGBAColor(0, 0, 0, 60),
                cx - displaySize * 0.15f, cy + displaySize * 0.45f,
                displaySize * 0.30f, 14);

            // Idle vertical bob applied to sprite only
            float bob = (float)Math.Sin(SplashKit.TimerTicks("blink") / 400.0) * 2f;
            cy += bob;

            if (_monsterSprites.TryGetValue(name, out Bitmap? bmp))
            {
                // SplashKit scales from the centre of the original bitmap, not from (x,y).
                // To render centred at (cx, cy) we must offset by half the ORIGINAL bitmap
                // dimensions so the scale-from-centre transform lands at (cx, cy).
                float drawX = cx - bmp.Width  / 2f;
                float drawY = cy - bmp.Height / 2f;
                SplashKit.DrawBitmap(bmp, drawX, drawY,
                    SplashKit.OptionScaleBmp(bmpScale, bmpScale));
            }
            else
            {
                DrawGenericMonster(cx, cy, scale);
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

        private void DrawInfoPanel(Monster m, int x, int y, int w, int h, bool isPlayer, float displayHpPct = -1f)
        {
            // Team colours: blue = player, orange = enemy
            Color borderC    = isPlayer
                ? SplashKit.RGBColor( 60, 140, 220)
                : SplashKit.RGBColor(220, 130,  50);
            Color bgC        = isPlayer
                ? SplashKit.RGBAColor(  8,  18,  55, 238)
                : SplashKit.RGBAColor( 55,  16,   6, 238);
            Color headerTint = isPlayer
                ? SplashKit.RGBAColor( 60, 140, 220, 80)
                : SplashKit.RGBAColor(220, 130,  50, 80);
            string panelTag  = isPlayer ? "YOUR MONSTER" : "ENEMY";

            // Background + double border for stronger frame
            SplashKit.FillRectangle(bgC,     x,     y,     w,     h);
            SplashKit.DrawRectangle(borderC,  x,     y,     w,     h);
            SplashKit.DrawRectangle(borderC,  x + 1, y + 1, w - 2, h - 2);

            // Coloured header strip
            SplashKit.FillRectangle(headerTint, x + 1, y + 1, w - 2, 20);

            // Role label centred in header
            int tagW = SplashKit.TextWidth(panelTag, "Arial", 10);
            SplashKit.DrawText(panelTag, borderC, "Arial", 10, x + w / 2 - tagW / 2, y + 4);

            // Name + level (shifted down to clear header)
            string dispName = m.Name;
            int nameMaxW = w - 70;
            while (dispName.Length > 3 && SplashKit.TextWidth(dispName, "Arial", 15) > nameMaxW)
                dispName = dispName.Substring(0, dispName.Length - 4) + "...";
            SplashKit.DrawText(dispName,
                SplashKit.ColorWhite(), "Arial", 15, x + 10, y + 24);
            SplashKit.DrawText($"Lv.{m.Level}",
                SplashKit.RGBColor(170, 220, 170), "Arial", 11, x + w - 44, y + 27);

            // Type badges — dynamic width prevents overflow
            DrawTypeBadge(m.PrimaryType, x + 10, y + 44);
            if (m.HasDualType)
                DrawTypeBadge(m.SecondaryType, x + 10 + TypeBadgeWidth(m.PrimaryType) + 4, y + 44);

            // HP bar — use animated display value when provided
            int barW = w - 20;
            SplashKit.FillRectangle(SplashKit.RGBColor(40, 40, 40), x + 10, y + 66, barW, 10);
            float pct  = displayHpPct >= 0f ? displayHpPct : m.HpPercent;
            Color barC = pct > 0.5f  ? SplashKit.RGBColor( 80, 220,  80)
                       : pct > 0.25f ? SplashKit.RGBColor(240, 210,  40)
                       :                SplashKit.RGBColor(240,  60,  60);
            int fillW  = (int)(barW * pct);
            if (fillW > 0)
                SplashKit.FillRectangle(barC, x + 10, y + 66, fillW, 10);
            SplashKit.DrawRectangle(SplashKit.RGBColor(80, 80, 80), x + 10, y + 66, barW, 10);
            SplashKit.DrawText($"HP  {m.Hp} / {m.MaxHp}",
                SplashKit.RGBColor(180, 180, 180), "Arial", 10, x + 10, y + 79);

            // Status condition badge if any
            if (m.Status != StatusCondition.None)
            {
                SplashKit.FillRectangle(SplashKit.RGBColor(180, 60, 180),
                    x + w - 70, y + 76, 58, 16);
                SplashKit.DrawText(m.Status.ToString(),
                    SplashKit.ColorWhite(), "Arial", 9, x + w - 68, y + 79);
            }
        }

        private int TypeBadgeWidth(MonsterType t)
            => SplashKit.TextWidth(t.ToString(), "Arial", 10) + 12;

        private void DrawTypeBadge(MonsterType t, int x, int y)
        {
            int badgeW = TypeBadgeWidth(t);
            Color bg = TypeBadgeColor(t);
            SplashKit.FillRectangle(bg, x, y, badgeW, 16);
            SplashKit.DrawText(t.ToString(),
                SplashKit.ColorWhite(), "Arial", 10, x + 6, y + 3);
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

        private void DrawMovePanel(TamedMonster player, Monster? enemy)
        {
            // Panel: left half of bottom UI strip (x=16, y=452 to y=720)
            // Tips strip occupies y=430–452 above this panel
            SplashKit.FillRectangle(SplashKit.RGBAColor(20, 35, 15, 230), 16, 452, 556, 268);
            SplashKit.DrawRectangle(SplashKit.RGBColor(80, 180, 80),       16, 452, 556, 268);
            SplashKit.DrawText("Choose a move:",
                SplashKit.RGBColor(180, 220, 180), "Arial", 11, 26, 462);

            for (int i = 0; i < player.MoveSet.Length; i++)
            {
                int col = i % 2, row = i / 2;
                // 2-column grid: box w=260, h=90; column gap 12px; row step 102px
                int bx  = 26  + col * 272;
                int by  = 478 + row * 102;
                bool sel = (i == _player.SelectedMoveIndex);

                Move move = player.MoveSet[i];
                bool superEff = enemy != null
                    && move.GetMoveCategory() == "Attack"
                    && IsSuperEffective(move.MoveType, enemy);

                // Box fill — brighter when selected + super effective
                SplashKit.FillRectangle(
                    sel && superEff ? SplashKit.RGBColor(60, 120, 40)
                    : sel           ? SplashKit.RGBColor(50, 100, 40)
                                    : SplashKit.RGBColor(25,  55, 20),
                    bx, by, 260, 90);

                // Outer border — gold when selected
                SplashKit.DrawRectangle(
                    sel ? SplashKit.RGBColor(255, 215, 0)
                        : SplashKit.RGBColor(80, 160, 60),
                    bx, by, 260, 90);

                // Inner border — bright yellow inset when selected AND super effective
                if (sel && superEff)
                    SplashKit.DrawRectangle(SplashKit.RGBColor(255, 255, 100), bx + 3, by + 3, 254, 84);

                Color nameCol = sel ? SplashKit.RGBColor(255, 215, 0) : SplashKit.ColorWhite();

                // Truncate name to fit inside box
                string moveName = move.Name;
                int nameLimit = move.Power > 0 ? 170 : 235;
                while (moveName.Length > 3 && SplashKit.TextWidth(moveName, "Arial", 14) > nameLimit)
                    moveName = moveName.Substring(0, moveName.Length - 4) + "...";
                SplashKit.DrawText(moveName, nameCol, "Arial", 14, bx + 10, by + 10);

                SplashKit.DrawText(move.GetMoveCategory(),
                    SplashKit.RGBColor(160, 200, 160), "Arial", 11, bx + 10, by + 30);

                // Type badge — gold outline when selected and super effective
                int badgeX = bx + 10, badgeY = by + 52;
                if (sel && superEff)
                {
                    int bw = TypeBadgeWidth(move.MoveType);
                    SplashKit.DrawRectangle(SplashKit.RGBColor(255, 240, 80), badgeX - 2, badgeY - 2, bw + 4, 20);
                }
                DrawTypeBadge(move.MoveType, badgeX, badgeY);

                // Right-align PWR with 10px right margin
                if (move.Power > 0)
                {
                    string pwrText = $"PWR {move.Power}";
                    int pwrW = SplashKit.TextWidth(pwrText, "Arial", 10);
                    SplashKit.DrawText(pwrText,
                        SplashKit.RGBColor(140, 180, 140), "Arial", 10, bx + 250 - pwrW, by + 56);
                }
            }

            // Separator lines between move boxes
            if (player.MoveSet.Length > 1)
                SplashKit.DrawLine(SplashKit.RGBColor(50, 100, 40), 292, 478, 292, 670);
            if (player.MoveSet.Length > 2)
                SplashKit.DrawLine(SplashKit.RGBColor(50, 100, 40), 26, 574, 558, 574);
        }

        private static bool IsSuperEffective(MonsterType moveType, Monster defender)
        {
            float mult = TypeChart.GetMultiplier(moveType, defender.PrimaryType);
            if (defender.HasDualType)
                mult *= TypeChart.GetMultiplier(moveType, defender.SecondaryType);
            return mult >= 2.0f;
        }

        private static string GetTypeEffectivenessHint(Move move, Monster enemy)
        {
            float p = TypeChart.GetMultiplier(move.MoveType, enemy.PrimaryType);
            float s = enemy.HasDualType ? TypeChart.GetMultiplier(move.MoveType, enemy.SecondaryType) : 1.0f;
            float combined = p * s;

            if (combined >= 2.0f)
            {
                MonsterType weakType = p >= 2.0f ? enemy.PrimaryType : enemy.SecondaryType;
                return $"{move.MoveType} is super effective against {weakType}!";
            }
            if (combined <= 0.5f && combined > 0.0f)
            {
                MonsterType resistType = p <= 0.5f ? enemy.PrimaryType : enemy.SecondaryType;
                return $"{move.MoveType} is not very effective against {resistType}...";
            }
            return "";
        }

        private void DrawBattleTips(TamedMonster player, Monster enemy)
        {
            // Dedicated HUD strip between scene and bottom panels (y=430–452)
            // Not overlapping any panel; spans the full width of both panel areas
            SplashKit.FillRectangle(SplashKit.RGBAColor(8, 16, 6, 210), 16, 430, 1248, 22);
            SplashKit.DrawRectangle(SplashKit.RGBColor(45, 80, 45),      16, 430, 1248, 22);
            SplashKit.DrawText("Tips:", SplashKit.RGBColor(75, 115, 75), "Arial", 10, 24, 433);

            // Type effectiveness — Attack moves only
            Move selectedMove = player.MoveSet[_player.SelectedMoveIndex];
            if (selectedMove.GetMoveCategory() == "Attack")
            {
                string typeHint = GetTypeEffectivenessHint(selectedMove, enemy);
                if (typeHint.Length > 0)
                {
                    bool superEff = typeHint.Contains("super effective");
                    Color hintColor = superEff
                        ? SplashKit.RGBColor(100, 240, 100)
                        : SplashKit.RGBColor(220, 130,  60);
                    SplashKit.DrawText(typeHint, hintColor, "Arial", 10, 66, 433);
                }
            }

            // Capture chance — only for wild battles when player has a Capture Orb
            WildMonster? wild = _battleManager?.WildTarget;
            if (wild != null)
            {
                CaptureOrb? orb = _player.Bag.GetAllItems().OfType<CaptureOrb>().FirstOrDefault();
                if (orb != null)
                {
                    float chance = orb.ComputeCatchChance(wild);
                    string label = chance < 0.10f ? "Very Low"
                                 : chance < 0.20f ? "Low"
                                 : chance < 0.35f ? "Moderate"
                                 : chance < 0.50f ? "High"
                                 : "Very High";
                    Color captureColor = chance < 0.10f ? SplashKit.RGBColor(200,  80,  80)
                                       : chance < 0.20f ? SplashKit.RGBColor(220, 140,  60)
                                       : chance < 0.35f ? SplashKit.RGBColor(220, 200,  60)
                                       : chance < 0.50f ? SplashKit.RGBColor(100, 200, 100)
                                       :                  SplashKit.RGBColor( 60, 220, 120);
                    string captureText = $"Capture Chance: {label}";
                    int ctw = SplashKit.TextWidth(captureText, "Arial", 10);
                    SplashKit.DrawText(captureText, captureColor, "Arial", 10, 1264 - ctw - 8, 433);
                }
            }
        }

        private void DrawItemPanel()
        {
            bool isWild = _battleManager?.WildTarget != null;
            List<Item> items = _player.Bag.GetAllItems()
                .Where(i => isWild || !(i is CaptureOrb))
                .ToList();

            SplashKit.FillRectangle(SplashKit.RGBAColor(15, 28, 50, 230), 16, 452, 556, 268);
            SplashKit.DrawRectangle(SplashKit.RGBColor(60, 140, 220),      16, 452, 556, 268);
            SplashKit.DrawText("Bag:",
                SplashKit.RGBColor(140, 200, 255), "Arial", 11, 26, 462);
            SplashKit.DrawText("[C] Back",
                SplashKit.RGBColor(100, 140, 180), "Arial", 10, 508, 462);

            if (items.Count == 0)
            {
                SplashKit.DrawText("Bag is empty.",
                    SplashKit.RGBColor(200, 140, 80), "Arial", 13, 220, 582);
                return;
            }

            for (int i = 0; i < Math.Min(items.Count, 4); i++)
            {
                bool sel = (i == _selectedItemIndex);
                int  col = i % 2, row = i / 2;
                int  bx  = 26  + col * 272;
                int  by  = 478 + row * 124;

                SplashKit.FillRectangle(
                    sel ? SplashKit.RGBColor(25, 58, 110)
                        : SplashKit.RGBColor(15, 36, 70),
                    bx, by, 260, 108);
                SplashKit.DrawRectangle(
                    sel ? SplashKit.RGBColor(0, 190, 255)
                        : SplashKit.RGBColor(50, 100, 170),
                    bx, by, 260, 108);

                Item  item  = items[i];
                Color nameC = sel ? SplashKit.RGBColor(0, 220, 255) : SplashKit.ColorWhite();
                SplashKit.DrawText(item.Name, nameC, "Arial", 13, bx + 10, by + 12);
                string desc = item.GetDescription();
                if (desc.Length > 32) desc = desc.Substring(0, 29) + "...";
                SplashKit.DrawText(desc,
                    SplashKit.RGBColor(120, 170, 210), "Arial", 10, bx + 10, by + 38);
                SplashKit.DrawText($"Rarity: {item.Rarity}",
                    SplashKit.RGBColor(100, 140, 180), "Arial", 10, bx + 10, by + 60);
            }

            if (items.Count > 4)
                SplashKit.DrawText($"... and {items.Count - 4} more",
                    SplashKit.RGBColor(100, 140, 180), "Arial", 10, 26, 714);
        }

        private void DrawSwapPanel()
        {
            TamedMonster? active = _player.GetActiveMonster();

            SplashKit.FillRectangle(SplashKit.RGBAColor(30, 15, 50, 230), 16, 452, 556, 268);
            SplashKit.DrawRectangle(SplashKit.RGBColor(160, 80, 220),      16, 452, 556, 268);
            SplashKit.DrawText("Party — Send out:",
                SplashKit.RGBColor(210, 160, 255), "Arial", 11, 26, 462);
            SplashKit.DrawText("[X] Back",
                SplashKit.RGBColor(140, 100, 180), "Arial", 10, 508, 462);

            for (int i = 0; i < Math.Min(_player.Party.Count, 6); i++)
            {
                TamedMonster m      = _player.Party[i];
                bool         sel    = (i == _selectedSwapIndex);
                bool         isCurr = (m == active);
                bool         isDead = !m.IsAlive;
                int          col    = i % 2, row = i / 2;
                int          bx     = 26  + col * 272;
                int          by     = 478 + row * 78;

                Color bg  = isDead  ? SplashKit.RGBColor(40, 15, 40)
                          : isCurr  ? SplashKit.RGBColor(50, 25, 80)
                          : sel     ? SplashKit.RGBColor(60, 25, 100)
                                    : SplashKit.RGBColor(30, 12, 55);
                Color brd = isDead  ? SplashKit.RGBColor(80, 40, 80)
                          : isCurr  ? SplashKit.RGBColor(120, 80, 180)
                          : sel     ? SplashKit.RGBColor(200, 120, 255)
                                    : SplashKit.RGBColor(100, 60, 150);

                SplashKit.FillRectangle(bg,  bx, by, 260, 66);
                SplashKit.DrawRectangle(brd, bx, by, 260, 66);

                Color nameC = isDead  ? SplashKit.RGBColor(100, 70, 100)
                            : sel     ? SplashKit.RGBColor(210, 150, 255)
                                      : SplashKit.ColorWhite();
                string tag   = isCurr ? " [OUT]" : isDead ? " [FNT]" : "";
                SplashKit.DrawText(m.Name + tag, nameC, "Arial", 12, bx + 10, by + 10);
                SplashKit.DrawText($"Lv.{m.Level}   HP {m.Hp}/{m.MaxHp}",
                    SplashKit.RGBColor(160, 130, 200), "Arial", 10, bx + 10, by + 34);
            }
        }

        private void DrawBattleLog()
        {
            if (_battleManager == null) return;
            int logX = 590, logY = 452, logW = ScreenW - 606, logH = 268;
            SplashKit.FillRectangle(SplashKit.RGBAColor(10, 20, 8, 230), logX, logY, logW, logH);
            SplashKit.DrawRectangle(SplashKit.RGBColor(60, 140, 60),      logX, logY, logW, logH);
            SplashKit.DrawText("Battle Log",
                SplashKit.RGBColor(80, 130, 80), "Arial", 10, logX + 10, logY + 8);

            List<LogEntry> log = _battleManager.BattleLog;
            int wrapW = logW - 22;
            var wrapped = new List<(string text, LogEntryType type)>();
            foreach (LogEntry entry in log)
                foreach (string line in WrapText(entry.Text, "Arial", 10, wrapW))
                    wrapped.Add((line, entry.Type));

            int maxLines = 9;
            int wStart   = Math.Max(0, wrapped.Count - maxLines);
            for (int i = wStart; i < wrapped.Count; i++)
            {
                float alpha = 0.45f + (float)(i - wStart) / maxLines * 0.55f;
                int   a     = (int)(alpha * 255);
                var   (text, type) = wrapped[i];

                var color = type switch
                {
                    LogEntryType.PlayerMove    => SplashKit.RGBAColor(140, 200, 255, a),
                    LogEntryType.EnemyMove     => SplashKit.RGBAColor(255, 155, 70,  a),
                    LogEntryType.StatusDamage  => SplashKit.RGBAColor(255, 190, 55,  a),
                    LogEntryType.TurnSkip      => SplashKit.RGBAColor(225, 225, 75,  a),
                    LogEntryType.TurnSeparator => SplashKit.RGBAColor(100, 130, 100, Math.Min(a, 150)),
                    LogEntryType.Separator     => SplashKit.RGBAColor(80,  105, 80,  Math.Min(a, 120)),
                    LogEntryType.Capture       => SplashKit.RGBAColor(255, 215, 0,   a),
                    LogEntryType.Healing       => SplashKit.RGBAColor(90,  255, 140, a),
                    LogEntryType.EffectDamage   => SplashKit.RGBAColor(255, 75,  75,  a),
                    LogEntryType.EffectParalyse => SplashKit.RGBAColor(240, 230, 60,  a),
                    LogEntryType.EffectBurn     => SplashKit.RGBAColor(255, 110, 40,  a),
                    LogEntryType.EffectFreeze   => SplashKit.RGBAColor(120, 210, 255, a),
                    LogEntryType.EffectPoison   => SplashKit.RGBAColor(190, 80,  230, a),
                    _                          => SplashKit.RGBAColor(175, 185, 175, a),
                };

                SplashKit.DrawText(text, color, "Arial", 10,
                    logX + 10, logY + 26 + (i - wStart) * 26);
            }
        }

        private List<string> WrapText(string text, string font, int fontSize, int maxWidth)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) { lines.Add(""); return lines; }
            string[] words   = text.Split(' ');
            string   current = "";
            foreach (string word in words)
            {
                string test = current.Length == 0 ? word : current + " " + word;
                if (SplashKit.TextWidth(test, font, fontSize) <= maxWidth)
                    current = test;
                else
                {
                    if (current.Length > 0) lines.Add(current);
                    current = word;
                }
            }
            if (current.Length > 0) lines.Add(current);
            if (lines.Count == 0)   lines.Add("");
            return lines;
        }

        private void DrawPartyScreen()
        {
            SplashKit.FillRectangle(SplashKit.RGBColor(10, 30, 10), 0, 0, ScreenW, ScreenH);

            SplashKit.DrawText("Party",
                SplashKit.RGBColor(255, 215, 0), "Arial", 24, ScreenW / 2 - 30, 28);
            SplashKit.DrawLine(SplashKit.RGBColor(45, 106, 79), 60, 62, ScreenW - 60, 62);

            for (int i = 0; i < _player.Party.Count; i++)
            {
                TamedMonster m  = _player.Party[i];
                int          py = 72 + i * 98;
                int          pw = ScreenW - 160; // 1120

                SplashKit.FillRectangle(SplashKit.RGBColor(18, 45, 18),  80, py, pw, 90);
                SplashKit.DrawRectangle(SplashKit.RGBColor(80, 160, 80),  80, py, pw, 90);

                // Monster sprite
                DrawMonsterSprite(m.Name, 140, py + 50, false, 65f);

                SplashKit.DrawText($"{i+1}.  {m.Name}",
                    SplashKit.ColorWhite(), "Arial", 16, 210, py + 14);
                SplashKit.DrawText($"Lv.{m.Level}",
                    SplashKit.RGBColor(116, 198, 157), "Arial", 13, 210, py + 38);
                SplashKit.DrawText($"EXP: {m.Experience}",
                    SplashKit.RGBColor(116, 198, 157), "Arial", 12, 310, py + 38);
                SplashKit.DrawText(m.GetDescription(),
                    SplashKit.RGBColor(120, 160, 120), "Arial", 11, 210, py + 62);

                // HP bar — wider on larger screen
                int barX = 600, barW2 = 540;
                SplashKit.FillRectangle(SplashKit.RGBColor(40, 40, 40), barX, py + 20, barW2, 14);
                float pct  = m.HpPercent;
                Color barC = pct > 0.5f  ? SplashKit.RGBColor(80, 220, 80)
                           : pct > 0.25f ? SplashKit.RGBColor(240, 210, 40)
                                         : SplashKit.RGBColor(240, 60, 60);
                SplashKit.FillRectangle(barC, barX, py + 20, (int)(barW2 * pct), 14);
                SplashKit.DrawRectangle(SplashKit.RGBColor(80, 80, 80), barX, py + 20, barW2, 14);
                SplashKit.DrawText($"HP  {m.Hp} / {m.MaxHp}",
                    SplashKit.RGBColor(160, 200, 160), "Arial", 11, barX, py + 42);

                // Moves — 4 across in one row
                for (int mi = 0; mi < m.MoveSet.Length; mi++)
                {
                    int mx = barX + (mi % 4) * 136;
                    int my = py + 64;
                    SplashKit.DrawText($"- {m.MoveSet[mi].Name}",
                        SplashKit.RGBColor(140, 180, 140), "Arial", 10, mx, my);
                }
            }

            SplashKit.DrawText("P or Enter to return",
                SplashKit.RGBColor(100, 150, 100), "Arial", 12, ScreenW / 2 - 80, ScreenH - 40);
        }

        private void DrawCaptureScreen()
        {
            SplashKit.FillRectangle(SplashKit.RGBColor(5, 15, 40), 0, 0, ScreenW, ScreenH);

            // Soft aura behind the sprite
            SplashKit.FillEllipse(SplashKit.RGBAColor(255, 215, 0, 18),
                ScreenW / 2 - 180, 40, 360, 360);
            SplashKit.FillEllipse(SplashKit.RGBAColor(80, 200, 255, 12),
                ScreenW / 2 - 130, 80, 260, 260);

            int cx = ScreenW / 2;

            // Title
            string title = "CAPTURED!";
            int tw = SplashKit.TextWidth(title, "Arial", 48);
            SplashKit.DrawText(title, SplashKit.RGBColor(255, 215, 0), "Arial", 48, cx - tw / 2, 60);

            // Monster sprite — non-player scale (larger) centred at top half of screen
            DrawMonsterSprite(_capturedMonsterName, cx, 225, false, 120f);

            // Info card
            int cardX = cx - 280, cardY = 320, cardW = 560, cardH = 250;
            SplashKit.FillRectangle(SplashKit.RGBAColor(5, 18, 50, 230), cardX, cardY, cardW, cardH);
            SplashKit.DrawRectangle(SplashKit.RGBColor(255, 215, 0),     cardX,     cardY,     cardW,     cardH);
            SplashKit.DrawRectangle(SplashKit.RGBColor(160, 130, 30),    cardX + 3, cardY + 3, cardW - 6, cardH - 6);

            // Name
            string nameText = _capturedMonsterName;
            int nw = SplashKit.TextWidth(nameText, "Arial", 22);
            SplashKit.DrawText(nameText, SplashKit.ColorWhite(), "Arial", 22, cx - nw / 2, cardY + 16);

            // Level
            string lvText = $"Lv. {_capturedMonsterLevel}";
            int lw = SplashKit.TextWidth(lvText, "Arial", 13);
            SplashKit.DrawText(lvText, SplashKit.RGBColor(170, 220, 170), "Arial", 13, cx - lw / 2, cardY + 46);

            // Type badges centred
            int primaryW   = TypeBadgeWidth(_capturedMonsterPrimary);
            int secondaryW = _capturedHasDual ? TypeBadgeWidth(_capturedMonsterSecondary) : 0;
            int totalBW    = primaryW + (_capturedHasDual ? secondaryW + 6 : 0);
            int bStartX    = cx - totalBW / 2;
            DrawTypeBadge(_capturedMonsterPrimary, bStartX, cardY + 68);
            if (_capturedHasDual)
                DrawTypeBadge(_capturedMonsterSecondary, bStartX + primaryW + 6, cardY + 68);

            // Divider
            SplashKit.DrawLine(SplashKit.RGBColor(60, 100, 160),
                cardX + 20, cardY + 92, cardX + cardW - 20, cardY + 92);

            // Capture stats
            string hpText = $"HP at capture:  {_capturedHpAtCapture} / {_capturedMaxHp}";
            int hw = SplashKit.TextWidth(hpText, "Arial", 12);
            SplashKit.DrawText(hpText, SplashKit.RGBColor(160, 200, 220), "Arial", 12, cx - hw / 2, cardY + 106);

            string chanceText = $"Capture Chance:  {_captureChanceLabel}";
            int chw = SplashKit.TextWidth(chanceText, "Arial", 12);
            SplashKit.DrawText(chanceText, SplashKit.RGBColor(160, 200, 220), "Arial", 12, cx - chw / 2, cardY + 126);

            // Party result message — colour shifts for "released" case
            string msg   = _capturePartyMessage;
            int    msgW  = SplashKit.TextWidth(msg, "Arial", 16);
            Color  msgC  = msg.Contains("released")
                ? SplashKit.RGBColor(240, 180, 80)
                : SplashKit.RGBColor(100, 230, 255);
            SplashKit.DrawText(msg, msgC, "Arial", 16, cx - msgW / 2, cardY + 162);

            // Prompt
            string prompt = "Press Enter to continue";
            int    pw2    = SplashKit.TextWidth(prompt, "Arial", 12);
            SplashKit.DrawText(prompt, SplashKit.RGBColor(100, 150, 100), "Arial", 12,
                cx - pw2 / 2, ScreenH - 44);
        }

        private void DrawVictory()
        {
            SplashKit.FillRectangle(SplashKit.RGBColor(5, 20, 5), 0, 0, ScreenW, ScreenH);
            SplashKit.FillEllipse(SplashKit.RGBAColor(255, 215, 0, 30),
                ScreenW / 2 - 100, ScreenH / 2 - 100, 200, 200);
            SplashKit.DrawText("Victory!",
                SplashKit.RGBColor(255, 215, 0), "Arial", 56, ScreenW / 2 - 175, ScreenH / 2 - 80);
            SplashKit.DrawText("You are the Guardian of Sarawak!",
                SplashKit.ColorWhite(), "Arial", 20, ScreenW / 2 - 185, ScreenH / 2 + 0);
            SplashKit.DrawText("Naga Belian has been defeated.",
                SplashKit.RGBColor(116, 198, 157), "Arial", 14, ScreenW / 2 - 160, ScreenH / 2 + 32);
            SplashKit.DrawText("Press Enter to exit",
                SplashKit.RGBColor(100, 150, 100), "Arial", 12, ScreenW / 2 - 85, ScreenH / 2 + 95);
        }

        private void DrawGameOver()
        {
            SplashKit.FillRectangle(SplashKit.RGBColor(20, 5, 5), 0, 0, ScreenW, ScreenH);

            string title    = "Defeated!";
            string subtitle = "All your monsters have fainted...";
            string prompt   = "Press Enter to exit";

            SplashKit.DrawText(title,
                SplashKit.RGBColor(220, 60, 60), "Arial", 56,
                ScreenW / 2 - SplashKit.TextWidth(title,    "Arial", 56) / 2, ScreenH / 2 - 80);
            SplashKit.DrawText(subtitle,
                SplashKit.RGBColor(200, 180, 180), "Arial", 18,
                ScreenW / 2 - SplashKit.TextWidth(subtitle, "Arial", 18) / 2, ScreenH / 2);
            SplashKit.DrawText(prompt,
                SplashKit.RGBColor(120, 100, 100), "Arial", 12,
                ScreenW / 2 - SplashKit.TextWidth(prompt,   "Arial", 12) / 2, ScreenH / 2 + 65);
        }
    }
}
