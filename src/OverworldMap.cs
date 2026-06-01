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

        // Encounter cooldown — prevents instant re-trigger
        private int _encounterCooldown;
        private static Random _rng = new Random();

        // Events
        public bool         WildEncounterTriggered { get; private set; }
        public bool         TrainerEncounterTriggered { get; private set; }
        public AITrainer?   TriggeredTrainer { get; private set; }
        public bool         RegionExitTriggered { get; private set; }

        public OverworldPlayer Player => _player;

        public OverworldMap(string regionName, int[,] mapData,
                            List<TrainerNPC> trainers, bool hasExit = false)
        {
            _regionName = regionName;
            _trainers   = trainers;
            _hasExit    = hasExit;
            _tiles      = new Tile[MapWidth, MapHeight];

            // Parse map data
            for (int y = 0; y < MapHeight; y++)
                for (int x = 0; x < MapWidth; x++)
                    _tiles[x, y] = new Tile((TileType)mapData[y, x]);

            // Place player on a walkable tile — scan from centre outward
            _player = new OverworldPlayer(12 * TileSize, 10 * TileSize);
            PlacePlayerOnWalkableTile();
            _encounterCooldown = 0;
        }

        private void PlacePlayerOnWalkableTile()
        {
            // Search outward from centre until we find a walkable tile
            for (int radius = 0; radius < 10; radius++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int tx = 12 + dx;
                        int ty = 10 + dy;
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
            TriggeredTrainer          = null;

            if (_encounterCooldown > 0) _encounterCooldown--;

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

            TileType current = _tiles[tileX, tileY].Type;

            // Tall grass — random encounter chance
            if (current == TileType.TallGrass && _encounterCooldown == 0)
            {
                if (_rng.NextDouble() < 0.04) // 4% per frame in tall grass
                {
                    WildEncounterTriggered = true;
                    _encounterCooldown     = 120; // 2 second cooldown
                }
            }

            // Region exit
            if (current == TileType.RegionExit)
            {
                RegionExitTriggered = true;
            }

            // Trainer spots
            foreach (TrainerNPC npc in _trainers)
            {
                if (npc.Defeated) continue;
                float dist = MathF.Sqrt(
                    MathF.Pow(_player.X - npc.X, 2) +
                    MathF.Pow(_player.Y - npc.Y, 2)
                );
                if (dist < TileSize * 1.5f)
                {
                    TrainerEncounterTriggered = true;
                    TriggeredTrainer          = npc.Trainer;
                    npc.Defeated              = true;
                    break;
                }
            }
        }

        private void UpdateCamera()
        {
            int screenW = 800;
            int screenH = 600;

            _camX = _player.X - screenW / 2 + OverworldPlayer.Size / 2;
            _camY = _player.Y - screenH / 2 + OverworldPlayer.Size / 2;

            float maxCamX = MapWidth  * TileSize - screenW;
            float maxCamY = MapHeight * TileSize - screenH;

            if (_camX < 0)       _camX = 0;
            if (_camY < 0)       _camY = 0;
            if (_camX > maxCamX) _camX = maxCamX;
            if (_camY > maxCamY) _camY = maxCamY;
        }

        public void Draw(Window window)
        {
            // Draw tiles
            for (int y = 0; y < MapHeight; y++)
            {
                for (int x = 0; x < MapWidth; x++)
                {
                    float screenX = x * TileSize - _camX;
                    float screenY = y * TileSize - _camY;

                    // Skip off-screen tiles
                    if (screenX < -TileSize || screenX > 832 ||
                        screenY < -TileSize || screenY > 632) continue;

                    DrawTile(_tiles[x, y].Type, screenX, screenY);
                }
            }

            // Draw trainer NPCs
            foreach (TrainerNPC npc in _trainers)
            {
                if (npc.Defeated) continue;
                float sx = npc.X - _camX;
                float sy = npc.Y - _camY;
                if (sx < -32 || sx > 832 || sy < -32 || sy > 632) continue;
                DrawTrainerNPC(sx, sy, npc.Label);
            }

            // Draw player
            float px = _player.X - _camX;
            float py = _player.Y - _camY;
            DrawPlayerSprite(px, py, _player.Direction, _player.AnimFrame);

            // Draw HUD overlay
            DrawHUD(window);
        }

        private void DrawTile(TileType type, float x, float y)
        {
            float s = TileSize;
            switch (type)
            {
                case TileType.Grass:
                    SplashKit.FillRectangle(SplashKit.RGBColor(76, 153, 0),   x, y, s, s);
                    SplashKit.DrawRectangle(SplashKit.RGBColor(60, 130, 0),   x, y, s, s);
                    break;

                case TileType.TallGrass:
                    SplashKit.FillRectangle(SplashKit.RGBColor(34, 120, 15),  x, y, s, s);
                    // Tall grass tufts
                    SplashKit.FillTriangle(SplashKit.RGBColor(50, 160, 20),
                        x+4,  y+s, x+8,  y+s-10, x+12, y+s);
                    SplashKit.FillTriangle(SplashKit.RGBColor(50, 160, 20),
                        x+14, y+s, x+18, y+s-12, x+22, y+s);
                    SplashKit.FillTriangle(SplashKit.RGBColor(60, 180, 25),
                        x+9,  y+s, x+13, y+s-14, x+17, y+s);
                    SplashKit.DrawRectangle(SplashKit.RGBColor(28, 100, 10),  x, y, s, s);
                    break;

                case TileType.Tree:
                    SplashKit.FillRectangle(SplashKit.RGBColor(20, 80, 0),    x, y, s, s);
                    SplashKit.FillCircle(SplashKit.RGBColor(30, 120, 10),
                        x + s/2, y + s/2, s * 0.45f);
                    SplashKit.FillCircle(SplashKit.RGBColor(40, 150, 15),
                        x + s/2 - 4, y + s/2 - 4, s * 0.3f);
                    break;

                case TileType.Water:
                    SplashKit.FillRectangle(SplashKit.RGBColor(30, 144, 255), x, y, s, s);
                    SplashKit.DrawLine(SplashKit.RGBColor(100, 180, 255),
                        x+4, y+s*0.4f, x+s-4, y+s*0.4f);
                    SplashKit.DrawLine(SplashKit.RGBColor(100, 180, 255),
                        x+4, y+s*0.65f, x+s-4, y+s*0.65f);
                    break;

                case TileType.Path:
                    SplashKit.FillRectangle(SplashKit.RGBColor(210, 180, 120), x, y, s, s);
                    SplashKit.DrawRectangle(SplashKit.RGBColor(180, 150, 90),  x, y, s, s);
                    break;

                case TileType.TrainerSpot:
                    SplashKit.FillRectangle(SplashKit.RGBColor(210, 180, 120), x, y, s, s);
                    break;

                case TileType.RegionExit:
                    SplashKit.FillRectangle(SplashKit.RGBColor(255, 215, 0),  x, y, s, s);
                    SplashKit.DrawRectangle(SplashKit.RGBColor(200, 160, 0),   x, y, s, s);
                    SplashKit.DrawText("EXIT",
                        SplashKit.RGBColor(100, 60, 0), "Arial", 8, x+6, y+12);
                    break;

                case TileType.House:
                    SplashKit.FillRectangle(SplashKit.RGBColor(180, 100, 60), x, y, s, s);
                    SplashKit.FillTriangle(SplashKit.RGBColor(150, 60, 40),
                        x, y, x+s, y, x+s/2, y-s*0.4f);
                    break;
            }
        }

        private void DrawPlayerSprite(float x, float y, int dir, int frame)
        {
            float s = OverworldPlayer.Size;

            // Body
            SplashKit.FillRectangle(SplashKit.RGBColor(70, 130, 180),
                x + 4, y + 10, s - 8, s - 10);

            // Head
            SplashKit.FillCircle(SplashKit.RGBColor(255, 200, 150),
                x + s/2, y + 8, 8);

            // Hat
            SplashKit.FillRectangle(SplashKit.RGBColor(34, 85, 34),
                x + 3, y + 2, s - 6, 5);
            SplashKit.FillRectangle(SplashKit.RGBColor(34, 85, 34),
                x + 6, y - 1, s - 12, 5);

            // Legs — animated
            if (frame == 0)
            {
                SplashKit.FillRectangle(SplashKit.RGBColor(50, 50, 150),
                    x + 5, y + s - 8, 6, 8);
                SplashKit.FillRectangle(SplashKit.RGBColor(50, 50, 150),
                    x + s - 11, y + s - 6, 6, 6);
            }
            else
            {
                SplashKit.FillRectangle(SplashKit.RGBColor(50, 50, 150),
                    x + 5, y + s - 6, 6, 6);
                SplashKit.FillRectangle(SplashKit.RGBColor(50, 50, 150),
                    x + s - 11, y + s - 8, 6, 8);
            }

            // Eyes based on direction
            if (dir == 0) // down
            {
                SplashKit.FillCircle(SplashKit.ColorBlack(), x + s/2 - 3, y + 8, 2);
                SplashKit.FillCircle(SplashKit.ColorBlack(), x + s/2 + 3, y + 8, 2);
            }
            else if (dir == 1) // up — no eyes visible
            {
            }
            else // left or right — one eye
            {
                SplashKit.FillCircle(SplashKit.ColorBlack(), x + s/2, y + 8, 2);
            }
        }

        private void DrawTrainerNPC(float x, float y, string label)
        {
            float s = OverworldPlayer.Size;

            // Body
            SplashKit.FillRectangle(SplashKit.RGBColor(180, 60, 60),
                x + 4, y + 10, s - 8, s - 10);

            // Head
            SplashKit.FillCircle(SplashKit.RGBColor(255, 200, 150),
                x + s/2, y + 8, 8);

            // Hat
            SplashKit.FillRectangle(SplashKit.RGBColor(120, 30, 30),
                x + 3, y + 2, s - 6, 5);

            // Eyes
            SplashKit.FillCircle(SplashKit.ColorBlack(), x + s/2 - 3, y + 8, 2);
            SplashKit.FillCircle(SplashKit.ColorBlack(), x + s/2 + 3, y + 8, 2);

            // Exclamation mark above
            SplashKit.DrawText("!", SplashKit.RGBColor(255, 215, 0), "Arial", 14,
                x + s/2 - 3, y - 16);

            // Name label
            SplashKit.DrawText(label, SplashKit.RGBColor(255, 255, 255), "Arial", 9,
                x - 10, y - 28);
        }

        private void DrawHUD(Window window)
        {
            // Top-left HUD — region info
            SplashKit.FillRectangle(SplashKit.RGBAColor(10, 40, 10, 200), 8, 8, 220, 64);
            SplashKit.DrawRectangle(SplashKit.RGBColor(74, 198, 100), 8, 8, 220, 64);
            SplashKit.DrawText(_regionName,
                SplashKit.RGBColor(255, 215, 0), "Arial", 14, 18, 18);
            SplashKit.DrawText("Walk into tall grass to encounter monsters",
                SplashKit.RGBColor(160, 220, 160), "Arial", 9, 18, 38);
            SplashKit.DrawText("Walk into a trainer (!) to battle",
                SplashKit.RGBColor(160, 220, 160), "Arial", 9, 18, 52);

            // Bottom controls bar
            SplashKit.FillRectangle(SplashKit.RGBAColor(10, 40, 10, 200),
                0, 572, 800, 28);
            SplashKit.DrawText(
                "WASD / Arrow keys: move     P: party screen     Esc: menu",
                SplashKit.RGBColor(160, 220, 160), "Arial", 10, 155, 580);
        }
    }

    // Map factory — builds the actual maps for each region
    public static class MapFactory
    {
        // T=Tree, G=Grass, g=TallGrass, W=Water, P=Path, N=TrainerSpot, X=Exit, H=House
        private const int T = (int)TileType.Tree;
        private const int G = (int)TileType.Grass;
        private const int g = (int)TileType.TallGrass;
        private const int W = (int)TileType.Water;
        private const int P = (int)TileType.Path;
        private const int N = (int)TileType.TrainerSpot;
        private const int X = (int)TileType.RegionExit;
        private const int H = (int)TileType.House;

        public static OverworldMap CreateRainforestMap(List<TrainerNPC> trainers)
        {
            int[,] map = new int[20, 25]
            {
                {T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T},
                {T,G,G,G,G,T,T,g,g,g,g,g,T,T,G,G,G,G,G,G,G,G,T,T,T},
                {T,G,H,H,G,G,T,g,g,g,g,g,T,G,G,G,G,G,G,G,G,G,G,T,T},
                {T,G,H,H,G,G,G,G,g,g,g,G,G,G,G,G,T,T,G,G,G,G,G,T,T},
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
            return new OverworldMap("Rainforest", map, trainers, hasExit: true);
        }

        public static OverworldMap CreateRiverDeltaMap(List<TrainerNPC> trainers)
        {
            int[,] map = new int[20, 25]
            {
                {T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T},
                {T,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,T,T},
                {T,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,T,T},
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
            return new OverworldMap("River Delta", map, trainers, hasExit: true);
        }

        public static OverworldMap CreateHighlandsMap(List<TrainerNPC> trainers)
        {
            int[,] map = new int[20, 25]
            {
                {T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T,T},
                {T,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,G,T,T},
                {T,G,G,G,G,T,T,G,G,G,G,G,G,G,G,G,T,T,G,G,G,G,G,T,T},
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
            return new OverworldMap("Ancient Highlands", map, trainers, hasExit: false);
        }
    }
}
