## Project structure

```
MonsterTamer/
├── MonsterTamer.csproj
└── src/
    ├── Program.cs
    ├── Enumerations.cs
    ├── Monster.cs
    ├── ConcreteMonsters.cs
    ├── Move.cs
    ├── ConcreteMoves.cs
    ├── Item.cs
    ├── ConcreteItems.cs
    ├── Trainer.cs
    ├── ConcreteTrainers.cs
    ├── TypeChart.cs
    ├── BattleState.cs
    ├── Party.cs
    ├── Inventory.cs
    ├── MonsterFactory.cs
    ├── Region.cs
    ├── BattleManager.cs
    ├── OverworldMap.cs
    └── GameManager.cs
```


# ARCHITECTURE & UML SPECIFICATIONS

## Class hierarchy overview

### Abstract base classes (4 total)

```
Monster (abstract)
├── TamedMonster        — player-owned, gains XP, has MoveSet
├── WildMonster         — catchable, has CatchRate, has MoveSet
└── BossMonster         — multi-phase, uncatchable, scripted phases

Move (abstract)         — evolved from Wizard Simulator Spell class
├── AttackMove          — deals damage using TypeChart multiplier
├── HealMove            — restores HP to caster
└── StatusMove          — inflicts StatusCondition on target

Item (abstract)
├── CaptureOrb          — attempts to catch WildMonster
├── Potion              — restores HP to TamedMonster
└── StatusCure          — removes StatusCondition from TamedMonster

Trainer (abstract)      — Strategy Pattern interface via ChooseAction()
├── PlayerTrainer       — reads keyboard input
└── AITrainer           — loops through MoveSet, picks best type advantage
```

### Plain classes (no abstract parent)

- `GameManager` — Singleton Pattern, top-level game loop
- `BattleManager` — runs one battle, calls Execute() and ChooseAction() polymorphically
- `Party` — holds up to 6 TamedMonster slots
- `Inventory` — holds Item objects with bag size limit
- `Region` — encounter tables and trainer rosters per area
- `BattleState` — snapshot passed to Trainer.ChooseAction()
- `TypeChart` — static 8×8 type advantage multiplier lookup table
- `MonsterFactory` — Factory Pattern, creates all Sarawak monsters
- `OverworldMap` — tile-based top-down map with camera, collision, triggers
- `OverworldPlayer` — player character position, direction, animation
- `TrainerNPC` — trainer sprite and position on overworld map

### Enumerations (7 total)

- `MonsterType` — Normal, Fire, Water, Grass, Electric, Flying, Fighting, Steel
- `GameState` — MainMenu, Overworld, Battle, PartyScreen, Victory, GameOver
- `TrainerAction` — UseMove, SwapMonster, UseItem, Forfeit
- `StatusCondition` — None, Burned, Frozen, Paralysed, Poisoned
- `CaptureResult` — Caught, Escaped, Failed
- `ItemRarity` — Common, Rare, Legendary
- `BattleResult` — PlayerWon, PlayerFled, PlayerDied, Ongoing


## Detailed class specifications

### Monster (abstract)

```
Fields (private):
  string _name
  int _hp
  int _maxHp
  int _level
  MonsterType _primaryType
  MonsterType _secondaryType
  bool _hasDualType
  StatusCondition _statusCondition

Properties (public):
  Name, Hp, MaxHp, Level, PrimaryType, SecondaryType
  HasDualType : bool (readonly)
  IsAlive : bool (readonly)
  HpPercent : float (readonly)
  Status : StatusCondition (get/set)

Concrete methods:
  TakeDamage(int amount) : void       — clamps HP to 0 minimum
  RestoreHp(int amount) : void        — clamps HP to MaxHp maximum
  LevelUp() : void                    — increments level, increases MaxHp
  ApplyStatusEffect() : void          — applies per-turn status damage

Abstract methods (must override in child classes):
  GetBattleCry() : string
  GetDescription() : string
```

### TamedMonster (child class, extends Monster)

```
Additional fields (private):
  string _battleCry
  string _description
  int _experience
  Move[] _moveSet

Additional properties:
  Experience : int (readonly)
  MoveSet : Move[] (readonly)

Additional methods:
  GainExperience(int amount) : void   — levels up when threshold reached

Overrides:
  GetBattleCry() : string
  GetDescription() : string
```

### WildMonster (child class, extends Monster)

```
Additional fields (private):
  string _battleCry
  string _description
  float _catchRate
  Move[] _moveSet

Additional properties:
  CatchRate : float (readonly)
  MoveSet : Move[] (readonly)

Overrides:
  GetBattleCry() : string
  GetDescription() : string
```

### BossMonster (child class, extends Monster)

```
Additional fields (private):
  string _battleCry
  int _phase
  int _totalPhases
  Move[] _moveSet

Additional properties:
  Phase : int (readonly)
  TotalPhases : int (readonly)
  MoveSet : Move[] (readonly)

Additional methods:
  UpdatePhase() : void                — recalculates phase based on HpPercent

Overrides:
  GetBattleCry() : string             — returns different cry per phase
  GetDescription() : string           — includes phase info
```

### Move (abstract)

```
Fields (private):
  string _name
  int _power
  int _manaCost
  MonsterType _moveType

Properties:
  Name, Power, ManaCost, MoveType (all readonly)

Abstract methods:
  Execute(Monster caster, Monster target) : string
  GetMoveCategory() : string
```

### AttackMove (child class, extends Move)

```
Overrides:
  Execute() — applies TypeChart multiplier, calls TakeDamage(), returns result string
  GetMoveCategory() — returns "Attack"
```

### HealMove (child class, extends Move)

```
Overrides:
  Execute() — calls RestoreHp() on caster, returns result string
  GetMoveCategory() — returns "Heal"
```

### StatusMove (child class, extends Move)

```
Additional field:
  StatusCondition _inflicts

Overrides:
  Execute() — sets target.Status if not already inflicted
  GetMoveCategory() — returns "Status"
```

### Item (abstract)

```
Fields (private):
  string _name
  string _description
  ItemRarity _rarity

Properties:
  Name, Rarity (readonly)

Abstract methods:
  Use(TamedMonster target) : void
  GetDescription() : string
```

### CaptureOrb (child class, extends Item)

```
Additional field:
  float _captureModifier

Additional method:
  AttemptCapture(WildMonster target) : CaptureResult
  — probability = (1 - HpPercent) * CatchRate * captureModifier

Overrides:
  Use() — placeholder (actual capture via AttemptCapture)
  GetDescription() : string
```

### Potion (child class, extends Item)

```
Additional field:
  int _healAmount

Overrides:
  Use() — calls target.RestoreHp(_healAmount)
  GetDescription() : string
```

### StatusCure (child class, extends Item)

```
Additional field:
  StatusCondition _cures

Overrides:
  Use() — clears target.Status if it matches _cures
  GetDescription() : string
```

### Trainer (abstract) — Strategy Pattern

```
Fields (private):
  string _name
  Party _party
  Inventory _bag

Properties:
  Name, Party, Bag (readonly)

Concrete methods:
  GetActiveMonster() : TamedMonster?  — returns first alive monster in party

Abstract method (Strategy Pattern interface):
  ChooseAction(BattleState state) : TrainerAction
```

### PlayerTrainer (child class, extends Trainer)

```
Additional field:
  int _selectedMoveIndex

Property:
  SelectedMoveIndex : int (get/set with clamping)

Overrides:
  ChooseAction() — returns UseMove (actual selection handled via input)
```

### AITrainer (child class, extends Trainer)

```
Additional fields:
  int _difficulty
  int _selectedMoveIndex

Property:
  SelectedMoveIndex : int (readonly)

Overrides:
  ChooseAction() — loops through MoveSet, picks move with best TypeChart multiplier
```

### BattleManager (class)

```
Fields (private):
  PlayerTrainer _player
  Trainer _opponent
  BattleResult _result
  int _turnNumber
  List<string> _battleLog
  WildMonster? _wildTarget

Properties:
  Result, BattleLog, TurnNumber, WildTarget

Methods:
  PlayerTurn(int moveIndex) : string
  EnemyTurn() : string
  AttemptCapture(CaptureOrb orb) : CaptureResult
  IsBattleOver() : bool
  AddLog(string message) : void
  ClearLog() : void
```

### Party (class)

```
Fields (private):
  List<TamedMonster> _monsters
  const int MaxSlots = 6

Properties:
  Count : int (readonly)
  this[int index] : TamedMonster (indexer)

Methods:
  AddMonster(TamedMonster monster) : bool
  RemoveMonster(TamedMonster monster) : void
  GetActiveMonster() : TamedMonster?
  IsWiped() : bool
```

### Inventory (class)

```
Fields (private):
  List<Item> _items
  int _maxSlots

Properties:
  Count : int (readonly)
  this[int index] : Item (indexer)

Methods:
  AddItem(Item item) : bool
  RemoveItem(Item item) : void
  GetAllItems() : List<Item>
```

### GameManager (class) — Singleton Pattern

```
Fields (private static):
  GameManager? _instance          — Singleton instance

Fields (private):
  Window _window
  GameState _state
  PlayerTrainer _player
  BattleManager? _battleManager
  List<Region> _regions
  int _currentRegionIndex
  OverworldMap? _overworldMap

Static method (Singleton access):
  GetInstance() : GameManager    — creates instance if null

Constructor: private GameManager()

Methods:
  Run() : void                   — main game loop
  HandleInput() : void
  Update() : void
  Draw() : void
  (private) SetupGame()
  (private) SetupRegions()
  (private) LoadOverworldMap(int regionIndex)
  (private) StartWildBattle(WildMonster)
  (private) StartTrainerBattle(AITrainer)
  (private) StartBossBattle()
  (private) EndBattle()
  (private) AdvanceRegion()
  (private) DrawMainMenu()
  (private) DrawOverworld()
  (private) DrawBattle()
  (private) DrawPartyScreen()
  (private) DrawVictory()
  (private) DrawGameOver()
  (private) DrawMonsterSprite(string name, float cx, float cy, bool isPlayer)
  (private) DrawInfoPanel(Monster, int x, int y, int w, int h, bool isPlayer)
  (private) DrawTypeBadge(MonsterType, int x, int y)
  (private) DrawMovePanel(TamedMonster)
  (private) DrawBattleLog()
  (private) DrawHpBar(Monster, int x, int y, int width)
```

### TypeChart (static class)

```
Static field:
  float[,] _chart — 8x8 multiplier table (rows=attacking type, cols=defending type)

Static method:
  GetMultiplier(MonsterType attackType, MonsterType defendType) : float
```

### MonsterFactory (static class) — Factory Pattern

```
Static methods (one per monster):
  CreateKelabit(int level = 12) : TamedMonster
  CreateBekantan(int level = 8) : WildMonster
  CreateMawas(int level = 10) : WildMonster
  CreateRajah(int level = 9) : WildMonster
  CreateEnggang(int level = 11) : WildMonster
  CreatePelanduk(int level = 6) : WildMonster
  CreateTemaju(int level = 13) : WildMonster
  CreateBuaya(int level = 14) : WildMonster
  CreateNagaBelian(int level = 30) : BossMonster
```

### Region (class)

```
Fields (private):
  string _name
  List<WildMonster> _encounterTable
  List<AITrainer> _trainers
  bool _isCleared

Properties:
  Name, IsCleared (readonly)
  Trainers : List<AITrainer> (readonly)

Methods:
  AddEncounter(WildMonster) : void
  AddTrainer(AITrainer) : void
  GetRandomEncounter() : WildMonster?
  GetNextTrainer() : AITrainer?
  Clear() : void
```

### OverworldMap (class)

```
Constants:
  TileSize = 32
  MapWidth = 25
  MapHeight = 20

Fields (private):
  Tile[,] _tiles
  OverworldPlayer _player
  List<TrainerNPC> _trainers
  string _regionName
  float _camX, _camY
  int _encounterCooldown

Properties:
  WildEncounterTriggered : bool
  TrainerEncounterTriggered : bool
  TriggeredTrainer : AITrainer?
  RegionExitTriggered : bool
  Player : OverworldPlayer

Methods:
  Update(bool up, bool down, bool left, bool right) : void
  Draw(Window window) : void
  (private) TryMove(float dx, float dy) : void
  (private) CheckTriggers() : void
  (private) UpdateCamera() : void
  (private) PlacePlayerOnWalkableTile() : void
  (private) DrawTile(TileType, float x, float y) : void
  (private) DrawPlayerSprite(float x, float y, int dir, int frame) : void
  (private) DrawTrainerNPC(float x, float y, string label) : void
  (private) DrawHUD(Window window) : void
```

### MapFactory (static class)

```
Static methods:
  CreateRainforestMap(List<TrainerNPC> trainers) : OverworldMap
  CreateRiverDeltaMap(List<TrainerNPC> trainers) : OverworldMap
  CreateHighlandsMap(List<TrainerNPC> trainers) : OverworldMap
```


## Design patterns implemented

### Singleton — GameManager

GameManager has a private static `_instance` field and a private constructor. `GetInstance()` is the only entry point. Enforces exactly one game loop, one player, one state machine.

### Factory — MonsterFactory

Static class with one creation method per Sarawak monster. Encapsulates all constructor parameters (name, HP, level, types, moves, battle cry, description). Callers never know construction details.

### Strategy — Trainer

`Trainer` defines abstract `ChooseAction(BattleState)` as the strategy interface. `PlayerTrainer` and `AITrainer` are concrete strategies. `BattleManager` calls `ChooseAction()` without knowing which implementation is active.


## Sequence diagram summary — HandleInput() battle turn

**Objects involved:** Player (actor), gameManager:GameManager, battleManager:BattleManager, move:Move, enemyMonster:Monster, aiTrainer:AITrainer

**Flow:**

1. Player presses Enter → `gameManager.HandleInput()`
2. `gameManager` → `battleManager.PlayerTurn(action)`
3. `battleManager` → `move.Execute(caster, target)`
4. `move` → `enemyMonster.TakeDamage(amount)` — void, no return
5. `move` → `battleManager` : return result string

**alt fragment** `[IsAlive() == true]` — enemy takes their turn:

- `battleManager` → `enemyMonster.IsAlive()` → return true
- **loop fragment** `[for each move in MoveSet]`:
  - `battleManager` → `aiTrainer.ChooseAction(battleState)` → return best move selected
- `battleManager` → `move.Execute(caster, target)` → return result string

**alt fragment** `[IsAlive() == false]` — battle ends:

- `battleManager` → `enemyMonster.IsAlive()` → return false — set BattleResult
- `battleManager` → `gameManager` : battle ended

16. `battleManager` → `gameManager` : return turn log
17. `gameManager` → Player : return updated battle screen