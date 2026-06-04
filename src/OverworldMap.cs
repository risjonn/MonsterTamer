using SplashKitSDK;
using System;
using System.Collections.Generic;

namespace MonsterTamer
{
    // Tile types for the overworld map
    public enum TileType
    {
        Grass,       // walkable open ground
        TallGrass,   // walkable — triggers encounters
        Tree,        // not walkable
        Water,       // not walkable
        Path,        // walkable stone path
        TrainerSpot, // walkable — triggers trainer battle
        RegionExit,  // walkable — moves to next region
        House,       // not walkable
        Shop,        // walkable — opens shop UI
    }

    // One tile on the map
    public class Tile
    {
        public TileType Type      { get; }
        public bool     Walkable  => Type != TileType.Tree
                                  && Type != TileType.Water
                                  && Type != TileType.House;

        public Tile(TileType type) { Type = type; }
    }

    // Player character in the overworld
    public class OverworldPlayer
    {
        private float _x;
        private float _y;
        private int   _direction; // 0=down 1=up 2=left 3=right
        private int   _animFrame;
        private int   _animTimer;
        public const int Size = 24;

        public float X         => _x;
        public float Y         => _y;
        public int   Direction => _direction;
        public int   AnimFrame => _animFrame;

        public OverworldPlayer(float x, float y)
        {
            _x         = x;
            _y         = y;
            _direction = 0;
            _animFrame = 0;
            _animTimer = 0;
        }

        public void Move(float dx, float dy)
        {
            _x += dx;
            _y += dy;

            if      (dy > 0) _direction = 0; // down
            else if (dy < 0) _direction = 1; // up
            else if (dx < 0) _direction = 2; // left
            else if (dx > 0) _direction = 3; // right

            _animTimer++;
            if (_animTimer >= 8)
            {
                _animFrame = (_animFrame + 1) % 2;
                _animTimer = 0;
            }
        }

        public void SetPosition(float x, float y)
        {
            _x = x;
            _y = y;
        }
    }

    // NPC trainer on the map
    public class TrainerNPC
    {
        public float      X         { get; }
        public float      Y         { get; }
        public AITrainer  Trainer   { get; }
        public bool       Defeated  { get; set; }
        public string     Label     { get; }

        public TrainerNPC(float x, float y, AITrainer trainer, string label)
        {
            X        = x;
            Y        = y;
            Trainer  = trainer;
            Defeated = false;
            Label    = label;
        }
    }

    // The overworld map and all its systems
    public class OverworldMap
    {
        public const int TileSize   = 32;
        public const int MapWidth   = 25;
        public const int MapHeight  = 20;

        private Tile[,]         _tiles;
        private OverworldPlayer _player;
        private List<TrainerNPC> _trainers;
        private string          _regionName;
        private bool            _hasExit;

        // Camera offset
        private float _camX;
        private float _camY;

        // Encounter cooldown and tile tracking
        private int _encounterCooldown;
        private int _lastTileX   = -1;
        private int _lastTileY   = -1;
        private static Random _rng = new Random();

        // Exit fade transition
        private bool _exitFading    = false;
        private int  _exitFadeAlpha = 0;

        // Cleared state
        private bool _isCleared = false;

        // Events
        public bool         WildEncounterTriggered    { get; private set; }
        public bool         TrainerEncounterTriggered { get; private set; }
        public AITrainer?   TriggeredTrainer          { get; private set; }
        public bool         RegionExitTriggered       { get; private set; }
        public bool         ShopEncounterTriggered    { get; private set; }

        public OverworldPlayer Player => _player;

        public OverworldMap(string regionName, int[,] mapData,
                            List<TrainerNPC> trainers, bool hasExit = false,
                            int spawnTileX = 12, int spawnTileY = 10)
        {
            _regionName = regionName;
            _trainers   = trainers;
            _hasExit    = hasExit;
            _tiles      = new Tile[MapWidth, MapHeight];

            for (int y = 0; y < MapHeight; y++)
                for (int x = 0; x < MapWidth; x++)
                    _tiles[x, y] = new Tile((TileType)mapData[y, x]);

            _player = new OverworldPlayer(spawnTileX * TileSize, spawnTileY * TileSize);
            PlacePlayerOnWalkableTile(spawnTileX, spawnTileY);
            _encounterCooldown = 0;
        }

        public void SetCleared(bool cleared) { _isCleared = cleared; }

        public void ApplyFleeingCooldown() { _encounterCooldown = 300; } // 5 s at 60fps

        public void MarkTrainerDefeated(AITrainer trainer)
        {
            foreach (TrainerNPC npc in _trainers)
            {
                if (npc.Trainer == trainer)
                {
                    npc.Defeated = true;
                    break;
                }
            }
        }

        private void PlacePlayerOnWalkableTile(int startTileX = 12, int startTileY = 10)
        {
            for (int radius = 0; radius < 10; radius++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int tx = startTileX + dx;
                        int ty = startTileY + dy;
                        if (tx < 0 || tx >= MapWidth || ty < 0 || ty >= MapHeight) continue;
                        if (_tiles[tx, ty].Walkable)
                        {
                            _player.SetPosition(tx * TileSize, ty * TileSize);
                            return;
                        }
                    }
                }
            }
        }

        public void Update(bool up, bool down, bool left, bool right)
        {
            WildEncounterTriggered    = false;
            TrainerEncounterTriggered = false;
            RegionExitTriggered       = false;
            ShopEncounterTriggered    = false;
            TriggeredTrainer          = null;

            if (_encounterCooldown > 0) _encounterCooldown--;

            // Advance exit fade; freeze input during transition
            if (_exitFading)
            {
                _exitFadeAlpha = Math.Min(255, _exitFadeAlpha + 8);
                if (_exitFadeAlpha >= 255)
                {
                    RegionExitTriggered = true;
                    _exitFading = false;
                }
                return;
            }

            float speed = 2.0f;
            float dx = 0, dy = 0;

            if (up)    dy = -speed;
            if (down)  dy =  speed;
            if (left)  dx = -speed;
            if (right) dx =  speed;

            if (dx != 0 && dy != 0)
            {
                dx *= 0.707f;
                dy *= 0.707f;
            }

            if (dx != 0 || dy != 0)
            {
                TryMove(dx, dy);
                CheckTriggers();
            }

            UpdateCamera();
        }

        private void TryMove(float dx, float dy)
        {
            float newX = _player.X + dx;
            float newY = _player.Y + dy;

            int tileX = (int)((newX + OverworldPlayer.Size / 2) / TileSize);
            int tileY = (int)((newY + OverworldPlayer.Size / 2) / TileSize);

            if (tileX < 0 || tileX >= MapWidth || tileY < 0 || tileY >= MapHeight)
                return;

            if (_tiles[tileX, tileY].Walkable)
                _player.Move(dx, dy);
        }

        private void CheckTriggers()
        {
            int tileX = (int)((_player.X + OverworldPlayer.Size / 2) / TileSize);
            int tileY = (int)((_player.Y + OverworldPlayer.Size / 2) / TileSize);

            if (tileX < 0 || tileX >= MapWidth || tileY < 0 || tileY >= MapHeight)
                return;

            TileType current  = _tiles[tileX, tileY].Type;
            bool     newTile  = (tileX != _lastTileX || tileY != _lastTileY);
            _lastTileX = tileX;
            _lastTileY = tileY;

            // Tall grass — 15% chance per new tile entered (was 4% per frame)
            if (current == TileType.TallGrass && newTile && _encounterCooldown == 0)
            {
                if (_rng.NextDouble() < 0.15)
                {
                    WildEncounterTriggered = true;
                    _encounterCooldown     = 60; // 1 second cooldown
                }
            }

            // Region exit — begin fade transition instead of instant trigger
            if (current == TileType.RegionExit && !_exitFading)
            {
                _exitFading    = true;
                _exitFadeAlpha = 0;
            }

            // Shop tile — trigger once per new tile entry
            if (current == TileType.Shop && newTile)
                ShopEncounterTriggered = true;

            // Trainer spots — only trigger when cooldown is clear
            foreach (TrainerNPC npc in _trainers)
            {
                if (npc.Defeated) continue;
                float dist = MathF.Sqrt(
                    MathF.Pow(_player.X - npc.X, 2) +
                    MathF.Pow(_player.Y - npc.Y, 2)
                );
                if (dist < TileSize * 1.5f && _encounterCooldown == 0)
                {
                    TrainerEncounterTriggered = true;
                    TriggeredTrainer          = npc.Trainer;
                    _encounterCooldown        = 60; // prevent immediate re-trigger; overwritten by ApplyFleeingCooldown on run
                    break;
                }
            }
        }

        // Screen constants matching GameManager.ScreenW / ScreenH
        private const int ScreenW = 1280;
        private const int ScreenH = 720;

        private void UpdateCamera()
        {
            float mapPixW = MapWidth  * TileSize; // 800
            float mapPixH = MapHeight * TileSize; // 640

            // When the map fits, use scale-to-fill in Draw — no camera offset needed
            if (mapPixW <= ScreenW)
            {
                _camX = 0;
            }
            else
            {
                _camX = _player.X - ScreenW / 2f + OverworldPlayer.Size / 2f;
                float maxCamX = mapPixW - ScreenW;
                if (_camX < 0)       _camX = 0;
                if (_camX > maxCamX) _camX = maxCamX;
            }

            if (mapPixH <= ScreenH)
            {
                _camY = 0;
            }
            else
            {
                _camY = _player.Y - ScreenH / 2f + OverworldPlayer.Size / 2f;
                float maxCamY = mapPixH - ScreenH;
                if (_camY < 0)       _camY = 0;
                if (_camY > maxCamY) _camY = maxCamY;
            }
        }

        public void Draw(Window window)
        {
            // Scale tiles to fill the full window (25×20 map = 800×640 < 1280×720)
            float mapPixW = MapWidth  * TileSize;
            float mapPixH = MapHeight * TileSize;
            float scaleX  = mapPixW < ScreenW ? (float)ScreenW / mapPixW : 1f; // 1.6
            float scaleY  = mapPixH < ScreenH ? (float)ScreenH / mapPixH : 1f; // 1.125
            float drawW   = TileSize * scaleX;   // tile draw width  ≈ 51.2
            float drawH   = TileSize * scaleY;   // tile draw height = 36.0
            float spriteS = MathF.Min(scaleX, scaleY); // proportional sprite scale

            // Camera offset in draw-space (0 when map fits and scaling fills window)
            float offX = mapPixW < ScreenW ? 0f : _camX * scaleX;
            float offY = mapPixH < ScreenH ? 0f : _camY * scaleY;

            // Draw tiles
            for (int y = 0; y < MapHeight; y++)
            {
                for (int x = 0; x < MapWidth; x++)
                {
                    float screenX = x * drawW - offX;
                    float screenY = y * drawH - offY;
                    if (screenX < -drawW || screenX > ScreenW || screenY < -drawH || screenY > ScreenH) continue;
                    DrawTile(_tiles[x, y].Type, screenX, screenY, drawW, drawH);
                }
            }

            // Draw trainer NPCs
            foreach (TrainerNPC npc in _trainers)
            {
                if (npc.Defeated) continue;
                float sx = npc.X * scaleX - offX;
                float sy = npc.Y * scaleY - offY;
                if (sx < -32 || sx > ScreenW + 32 || sy < -32 || sy > ScreenH + 32) continue;
                DrawTrainerNPC(sx, sy, npc.Label, spriteS);
            }

            // Draw player
            float px = _player.X * scaleX - offX;
            float py = _player.Y * scaleY - offY;
            DrawPlayerSprite(px, py, _player.Direction, _player.AnimFrame, spriteS);

            // Draw HUD overlay
            DrawHUD(window);

            // Fade-to-black overlay during region exit transition
            if (_exitFadeAlpha > 0)
                SplashKit.FillRectangle(SplashKit.RGBAColor(0, 0, 0, _exitFadeAlpha), 0, 0, ScreenW, ScreenH);
        }

        private void DrawTile(TileType type, float x, float y, float w, float h)
        {
            // rx/ry scale internal offsets proportionally to the drawn tile size
            float rx = w / TileSize;
            float ry = h / TileSize;
            float cr = MathF.Min(w, h) * 0.45f; // circle radius for Tree
            switch (type)
            {
                case TileType.Grass:
                    SplashKit.FillRectangle(SplashKit.RGBColor(76, 153, 0),  x, y, w, h);
                    SplashKit.DrawRectangle(SplashKit.RGBColor(60, 130, 0),  x, y, w, h);
                    break;

                case TileType.TallGrass:
                    SplashKit.FillRectangle(SplashKit.RGBColor(34, 120, 15), x, y, w, h);
                    SplashKit.FillTriangle(SplashKit.RGBColor(50, 160, 20),
                        x+ 4*rx, y+h, x+ 8*rx, y+h-10*ry, x+12*rx, y+h);
                    SplashKit.FillTriangle(SplashKit.RGBColor(50, 160, 20),
                        x+14*rx, y+h, x+18*rx, y+h-12*ry, x+22*rx, y+h);
                    SplashKit.FillTriangle(SplashKit.RGBColor(60, 180, 25),
                        x+ 9*rx, y+h, x+13*rx, y+h-14*ry, x+17*rx, y+h);
                    SplashKit.DrawRectangle(SplashKit.RGBColor(28, 100, 10), x, y, w, h);
                    break;

                case TileType.Tree:
                    SplashKit.FillRectangle(SplashKit.RGBColor(20, 80, 0),   x, y, w, h);
                    SplashKit.FillCircle(SplashKit.RGBColor(30, 120, 10),
                        x + w/2, y + h/2, cr);
                    SplashKit.FillCircle(SplashKit.RGBColor(40, 150, 15),
                        x + w/2 - 4*rx, y + h/2 - 4*ry, cr * 0.65f);
                    break;

                case TileType.Water:
                    SplashKit.FillRectangle(SplashKit.RGBColor(30, 144, 255), x, y, w, h);
                    SplashKit.DrawLine(SplashKit.RGBColor(100, 180, 255),
                        x+4*rx, y+h*0.4f, x+w-4*rx, y+h*0.4f);
                    SplashKit.DrawLine(SplashKit.RGBColor(100, 180, 255),
                        x+4*rx, y+h*0.65f, x+w-4*rx, y+h*0.65f);
                    break;

                case TileType.Path:
                    SplashKit.FillRectangle(SplashKit.RGBColor(210, 180, 120), x, y, w, h);
                    SplashKit.DrawRectangle(SplashKit.RGBColor(180, 150, 90),  x, y, w, h);
                    break;

                case TileType.TrainerSpot:
                    SplashKit.FillRectangle(SplashKit.RGBColor(210, 180, 120), x, y, w, h);
                    break;

                case TileType.RegionExit:
                    if (_isCleared)
                    {
                        bool pulse = (SplashKit.TimerTicks("blink") % 600) < 350;
                        SplashKit.FillRectangle(
                            pulse ? SplashKit.RGBColor(255, 235, 0) : SplashKit.RGBColor(210, 170, 0),
                            x, y, w, h);
                        SplashKit.DrawRectangle(SplashKit.RGBColor(255, 255, 100), x, y, w, h);
                        SplashKit.DrawText("EXIT", SplashKit.RGBColor(60, 30, 0), "Arial", 8,
                            x+4*rx, y+6*ry);
                        SplashKit.FillTriangle(SplashKit.RGBColor(60, 30, 0),
                            x+w/2-6*rx, y+18*ry, x+w/2+6*rx, y+18*ry, x+w/2, y+28*ry);
                    }
                    else
                    {
                        SplashKit.FillRectangle(SplashKit.RGBColor(180, 150, 0), x, y, w, h);
                        SplashKit.DrawRectangle(SplashKit.RGBColor(130, 100, 0), x, y, w, h);
                        SplashKit.DrawText("EXIT", SplashKit.RGBColor(80, 50, 0), "Arial", 8,
                            x+6*rx, y+12*ry);
                    }
                    break;

                case TileType.House:
                    SplashKit.FillRectangle(SplashKit.RGBColor(180, 100, 60), x, y, w, h);
                    SplashKit.FillTriangle(SplashKit.RGBColor(150, 60, 40),
                        x, y, x+w, y, x+w/2, y-h*0.4f);
                    break;

                case TileType.Shop:
                    SplashKit.FillRectangle(SplashKit.RGBColor(160, 120, 80), x, y, w, h);
                    SplashKit.FillTriangle(SplashKit.RGBColor(255, 180, 30),
                        x, y, x+w, y, x+w/2, y-h*0.4f);
                    SplashKit.DrawText("$", SplashKit.RGBColor(255, 215, 0), "Arial", 10,
                        x + w*0.3f, y + h*0.2f);
                    break;
            }
        }

        private void DrawPlayerSprite(float x, float y, int dir, int frame, float scale = 1.0f)
        {
            float os = OverworldPlayer.Size; // original size constant
            float s  = os * scale;           // scaled size

            // Body
            SplashKit.FillRectangle(SplashKit.RGBColor(70, 130, 180),
                x + 4*scale, y + 10*scale, (os-8)*scale, (os-10)*scale);

            // Head
            SplashKit.FillCircle(SplashKit.RGBColor(255, 200, 150),
                x + s/2, y + 8*scale, 8*scale);

            // Hat
            SplashKit.FillRectangle(SplashKit.RGBColor(34, 85, 34),
                x + 3*scale, y + 2*scale, (os-6)*scale, 5*scale);
            SplashKit.FillRectangle(SplashKit.RGBColor(34, 85, 34),
                x + 6*scale, y - scale, (os-12)*scale, 5*scale);

            // Legs — animated
            if (frame == 0)
            {
                SplashKit.FillRectangle(SplashKit.RGBColor(50, 50, 150),
                    x + 5*scale, y + (os-8)*scale, 6*scale, 8*scale);
                SplashKit.FillRectangle(SplashKit.RGBColor(50, 50, 150),
                    x + (os-11)*scale, y + (os-6)*scale, 6*scale, 6*scale);
            }
            else
            {
                SplashKit.FillRectangle(SplashKit.RGBColor(50, 50, 150),
                    x + 5*scale, y + (os-6)*scale, 6*scale, 6*scale);
                SplashKit.FillRectangle(SplashKit.RGBColor(50, 50, 150),
                    x + (os-11)*scale, y + (os-8)*scale, 6*scale, 8*scale);
            }

            // Eyes based on direction
            if (dir == 0) // down
            {
                SplashKit.FillCircle(SplashKit.ColorBlack(), x + s/2 - 3*scale, y + 8*scale, 2*scale);
                SplashKit.FillCircle(SplashKit.ColorBlack(), x + s/2 + 3*scale, y + 8*scale, 2*scale);
            }
            else if (dir == 1) // up — no eyes visible
            {
            }
            else // left or right — one eye
            {
                SplashKit.FillCircle(SplashKit.ColorBlack(), x + s/2, y + 8*scale, 2*scale);
            }
        }

        private void DrawTrainerNPC(float x, float y, string label, float scale = 1.0f)
        {
            float os = OverworldPlayer.Size;
            float s  = os * scale;

            // Body
            SplashKit.FillRectangle(SplashKit.RGBColor(180, 60, 60),
                x + 4*scale, y + 10*scale, (os-8)*scale, (os-10)*scale);

            // Head
            SplashKit.FillCircle(SplashKit.RGBColor(255, 200, 150),
                x + s/2, y + 8*scale, 8*scale);

            // Hat
            SplashKit.FillRectangle(SplashKit.RGBColor(120, 30, 30),
                x + 3*scale, y + 2*scale, (os-6)*scale, 5*scale);

            // Eyes
            SplashKit.FillCircle(SplashKit.ColorBlack(), x + s/2 - 3*scale, y + 8*scale, 2*scale);
            SplashKit.FillCircle(SplashKit.ColorBlack(), x + s/2 + 3*scale, y + 8*scale, 2*scale);

            // Exclamation mark above
            SplashKit.DrawText("!", SplashKit.RGBColor(255, 215, 0), "Arial", 14,
                x + s/2 - 3*scale, y - 16*scale);

            // Name label
            SplashKit.DrawText(label, SplashKit.RGBColor(255, 255, 255), "Arial", 9,
                x - 10, y - 28*scale);
        }

        private void DrawHUD(Window window)
        {
            // Top-left HUD — expand slightly when cleared to fit badge
            int hudH = _isCleared ? 82 : 64;
            SplashKit.FillRectangle(SplashKit.RGBAColor(10, 40, 10, 200), 8, 8, 280, hudH);
            SplashKit.DrawRectangle(
                _isCleared ? SplashKit.RGBColor(0, 255, 100) : SplashKit.RGBColor(74, 198, 100),
                8, 8, 280, hudH);
            SplashKit.DrawText(_regionName,
                SplashKit.RGBColor(255, 215, 0), "Arial", 14, 18, 18);

            if (_isCleared)
            {
                // Green "CLEARED" badge
                SplashKit.FillRectangle(SplashKit.RGBColor(0, 160, 70), 18, 38, 200, 22);
                SplashKit.DrawRectangle(SplashKit.RGBColor(0, 255, 100), 18, 38, 200, 22);
                SplashKit.DrawText("REGION CLEARED!",
                    SplashKit.ColorWhite(), "Arial", 11, 24, 42);
                SplashKit.DrawText("Walk to the EXIT tile to continue",
                    SplashKit.RGBColor(160, 255, 160), "Arial", 9, 18, 68);
            }
            else
            {
                SplashKit.DrawText("Tall grass - wild encounters",
                    SplashKit.RGBColor(160, 220, 160), "Arial", 9, 18, 38);
                SplashKit.DrawText("Walk into a trainer (!) to battle",
                    SplashKit.RGBColor(160, 220, 160), "Arial", 9, 18, 52);
            }

            // Bottom controls bar
            SplashKit.FillRectangle(SplashKit.RGBAColor(10, 40, 10, 200), 0, ScreenH - 28, ScreenW, 28);
            SplashKit.DrawText(
                "WASD / Arrow keys: move     P: party screen     Esc: menu",
                SplashKit.RGBColor(160, 220, 160), "Arial", 10, ScreenW / 2 - 230, ScreenH - 20);
        }
    }

    // Map factory — builds the actual maps for each region
    public static class MapFactory
    {
        // T=Tree, G=Grass, g=TallGrass, W=Water, P=Path, N=TrainerSpot, X=Exit, H=House, S=Shop
        private const int T = (int)TileType.Tree;
        private const int G = (int)TileType.Grass;
        private const int g = (int)TileType.TallGrass;
        private const int W = (int)TileType.Water;
        private const int P = (int)TileType.Path;
        private const int N = (int)TileType.TrainerSpot;
        private const int X = (int)TileType.RegionExit;
        private const int H = (int)TileType.House;
        private const int S = (int)TileType.Shop;

        public static OverworldMap CreateRainforestMap(List<TrainerNPC> trainers)
        {
            int[,] map = new int[20, 25]
            {
                {T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T},
                {T,G,G,G,G,T,T,g,g,g,g,g,T,T,G,G,G,G,G,G,G,G,T,T,T},
                {T,G,H,H,G,G,T,g,g,g,g,g,T,G,G,G,G,G,G,G,G,G,G,T,T},
                {T,G,H,H,S,G,G,G,g,g,g,G,G,G,G,G,T,T,G,G,G,G,G,T,T},
                {T,G,G,G,G,G,G,G,G,G,G,G,G,G,G,T,T,T,G,G,G,G,G,T,T},
                {T,T,G,G,P,P,P,P,P,P,P,P,P,P,G,T,T,G,G,G,G,g,g,T,T},
                {T,T,G,G,P,T,T,T,T,T,T,T,T,P,G,G,G,G,g,g,g,g,g,T,T},
                {T,T,G,G,P,T,g,g,g,g,g,g,T,P,G,G,G,G,G,g,g,g,G,T,T},
                {T,T,G,G,P,T,g,g,g,g,g,g,T,P,G,G,G,N,G,G,g,G,G,T,T},
                {T,T,G,G,P,T,g,g,g,g,g,g,T,P,G,G,G,G,G,G,G,G,G,T,T},
                {T,W,W,G,P,T,T,T,T,T,T,T,T,P,G,G,W,W,W,W,G,G,G,T,T},
                {T,W,W,G,P,P,P,P,P,P,P,P,P,P,G,G,W,W,W,W,G,G,G,T,T},
                {T,W,W,G,G,G,G,G,G,G,G,G,G,G,G,G,W,W,W,W,G,G,G,T,T},
                {T,T,G,G,G,G,g,g,g,G,G,G,G,G,G,G,G,G,G,G,G,G,G,T,T},
                {T,T,G,G,G,G,g,g,g,G,G,G,G,G,G,G,G,G,G,G,G,G,G,T,T},
                {T,T,T,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,T,T,T},
                {T,T,T,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,T,T,T},
                {T,T,T,T,G,G,G,G,G,G,G,G,P,P,P,P,P,G,G,G,G,T,T,T,T},
                {T,T,T,T,T,G,G,G,G,G,G,G,P,X,X,X,P,G,G,G,T,T,T,T,T},
                {T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T},
            };
            return new OverworldMap("Rainforest", map, trainers, hasExit: true, spawnTileX: 4, spawnTileY: 2);
        }

        public static OverworldMap CreateRiverDeltaMap(List<TrainerNPC> trainers)
        {
            int[,] map = new int[20, 25]
            {
                {T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T},
                {T,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,T,T},
                {T,G,G,S,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,T,T},
                {T,G,G,T,T,G,G,G,G,g,g,g,G,G,G,G,G,T,T,G,G,G,G,T,T},
                {T,G,G,T,T,G,G,G,G,g,g,g,G,G,G,G,T,T,T,G,G,G,G,T,T},
                {T,G,G,G,G,G,G,G,G,G,G,G,G,G,G,T,T,T,G,G,G,G,G,T,T},
                {T,G,G,G,P,P,P,P,P,G,G,G,G,G,T,T,T,G,G,G,G,G,G,T,T},
                {T,G,G,G,P,T,T,T,P,G,G,G,G,T,T,T,G,G,G,N,G,G,G,T,T},
                {T,W,W,G,P,T,g,g,P,G,G,G,T,T,T,G,G,G,G,G,G,G,G,T,T},
                {T,W,W,W,P,T,g,g,P,G,G,T,T,T,G,G,G,G,G,G,G,G,G,T,T},
                {T,W,W,W,P,T,T,T,P,P,P,P,P,G,G,G,G,G,g,g,g,G,G,T,T},
                {T,W,W,G,G,G,G,G,G,G,G,G,P,G,G,G,G,G,g,g,g,G,G,T,T},
                {T,G,G,G,G,G,G,G,G,G,G,G,P,G,G,G,G,G,G,G,G,G,G,T,T},
                {T,G,G,G,G,G,G,g,g,G,G,G,P,G,G,G,G,G,G,G,G,G,T,T,T},
                {T,G,G,G,G,G,G,g,g,G,G,G,P,G,G,G,G,G,G,G,G,T,T,T,T},
                {T,T,G,G,G,G,G,G,G,G,G,G,P,P,P,G,G,G,G,G,T,T,T,T,T},
                {T,T,T,G,G,G,G,G,G,G,G,G,G,G,P,G,G,G,G,T,T,T,T,T,T},
                {T,T,T,T,G,G,G,G,G,G,G,G,G,G,P,G,G,G,T,T,T,T,T,T,T},
                {T,T,T,T,T,G,G,G,G,G,G,G,G,G,X,G,G,T,T,T,T,T,T,T,T},
                {T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T},
            };
            return new OverworldMap("River Delta", map, trainers, hasExit: true, spawnTileX: 2, spawnTileY: 2);
        }

        public static OverworldMap CreateHighlandsMap(List<TrainerNPC> trainers)
        {
            int[,] map = new int[20, 25]
            {
                {T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T},
                {T,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,T,T},
                {T,G,G,S,G,T,T,G,G,G,G,G,G,G,G,G,T,T,G,G,G,G,G,T,T},
                {T,G,G,T,T,T,T,G,g,g,g,G,G,G,G,T,T,T,G,G,G,G,G,T,T},
                {T,G,G,T,T,T,G,G,g,g,g,G,G,G,T,T,T,G,G,G,G,G,G,T,T},
                {T,G,G,G,G,G,G,G,G,G,G,G,G,T,T,T,G,G,G,G,g,g,G,T,T},
                {T,G,G,G,G,G,G,G,G,G,G,G,T,T,T,G,G,G,G,G,g,g,G,T,T},
                {T,G,G,G,P,P,P,P,G,G,G,T,T,T,G,G,G,G,G,G,G,G,G,T,T},
                {T,G,G,G,P,T,T,P,G,G,T,T,T,G,G,G,G,G,G,N,G,G,G,T,T},
                {T,G,G,G,P,T,T,P,G,T,T,T,G,G,G,G,G,G,G,G,G,G,G,T,T},
                {T,G,G,G,P,T,T,P,G,G,G,G,G,G,G,G,g,g,G,G,G,G,G,T,T},
                {T,G,G,G,P,P,P,P,G,G,G,G,G,G,G,G,g,g,G,G,G,G,G,T,T},
                {T,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,T,T},
                {T,T,G,G,G,G,G,G,g,g,G,G,G,G,G,G,G,G,G,G,G,G,T,T,T},
                {T,T,G,G,G,G,G,G,g,g,G,G,G,G,G,G,G,G,G,G,G,T,T,T,T},
                {T,T,T,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,T,T,T,T,T},
                {T,T,T,T,G,G,G,G,G,G,G,G,P,P,P,G,G,G,G,T,T,T,T,T,T},
                {T,T,T,T,T,G,G,G,G,G,G,G,P,G,P,G,G,G,T,T,T,T,T,T,T},
                {T,T,T,T,T,T,G,G,G,G,G,G,P,X,P,G,G,T,T,T,T,T,T,T,T},
                {T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T},
            };
            return new OverworldMap("Ancient Highlands", map, trainers, hasExit: false, spawnTileX: 2, spawnTileY: 2);
        }
    }
}
