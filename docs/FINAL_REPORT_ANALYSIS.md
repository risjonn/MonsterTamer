# MonsterTamer — Architecture Analysis

> Scope: `src/` folder (21 files). Focused on structure, relationships, and responsibilities.
> Excludes rendering code, animation implementation details, and UI formatting.

---

## 1. Complete Class List

### Abstract Base Classes
| Class | File |
|---|---|
| `Monster` | Monster.cs |
| `Move` | Move.cs |
| `Item` | Item.cs |
| `Trainer` | Trainer.cs |
| `BattleAnimation` | BattleAnimation.cs |

### Concrete Domain Classes
| Class | Extends | File |
|---|---|---|
| `TamedMonster` | `Monster` | ConcreteMonsters.cs |
| `WildMonster` | `Monster` | ConcreteMonsters.cs |
| `BossMonster` | `Monster` | ConcreteMonsters.cs |
| `AttackMove` | `Move` | ConcreteMoves.cs |
| `HealMove` | `Move` | ConcreteMoves.cs |
| `StatusMove` | `Move` | ConcreteMoves.cs |
| `CaptureOrb` | `Item` | ConcreteItems.cs |
| `Potion` | `Item` | ConcreteItems.cs |
| `StatusCure` | `Item` | ConcreteItems.cs |
| `PlayerTrainer` | `Trainer` | ConcreteTrainers.cs |
| `AITrainer` | `Trainer` | ConcreteTrainers.cs |

### Concrete Support Classes
| Class | File |
|---|---|
| `BattleManager` | BattleManager.cs |
| `BattleState` | BattleState.cs |
| `GameManager` | GameManager.cs |
| `Party` | Party.cs |
| `Inventory` | Inventory.cs |
| `Region` | Region.cs |
| `LogEntry` *(readonly struct)* | LogEntry.cs |
| `Tile` | OverworldMap.cs |
| `OverworldPlayer` | OverworldMap.cs |
| `TrainerNPC` | OverworldMap.cs |
| `OverworldMap` | OverworldMap.cs |

### Static / Factory Classes
| Class | File |
|---|---|
| `MonsterFactory` | MonsterFactory.cs |
| `TypeChart` | TypeChart.cs |
| `MapFactory` | OverworldMap.cs |

### Animation Subclasses (rendering-only, omit from UML)
`HitFlashAnimation`, `DamageNumberAnimation`, `HealFlashAnimation`, `HealNumberAnimation`,
`HealParticleAnimation`, `StatusSwirlAnimation`, `CaptureOrbAnimation`, `PotionGlowAnimation`,
`TurnSkipAnimation` — all extend `BattleAnimation`.

---

## 2. Interfaces and Enums

### No Interfaces
The project uses abstract classes exclusively; no `interface` declarations exist.

### Enums — `Enumerations.cs`
| Enum | Values |
|---|---|
| `MonsterType` | Normal, Fire, Water, Grass, Electric, Flying, Fighting, Steel |
| `GameState` | MainMenu, Overworld, EncounterPrompt, Battle, PartyScreen, CaptureScreen, Victory, GameOver |
| `TrainerAction` | UseMove, SwapMonster, UseItem, Forfeit |
| `StatusCondition` | None, Burned, Frozen, Paralysed, Poisoned |
| `CaptureResult` | Caught, Escaped, Failed |
| `ItemRarity` | Common, Rare, Legendary |
| `BattleResult` | PlayerWon, PlayerFled, PlayerDied, Ongoing |
| `LogEntryType` | System, PlayerMove, EnemyMove, StatusDamage, TurnSkip, Separator, TurnSeparator, Capture, Healing, EffectDamage, EffectParalyse, EffectBurn, EffectFreeze, EffectPoison |

### Enum — `OverworldMap.cs`
| Enum | Values |
|---|---|
| `TileType` | Grass, TallGrass, Tree, Water, Path, TrainerSpot, RegionExit, House, Shop |

---

## 3. Inheritance Relationships

```
Monster (abstract)
├── TamedMonster          — player-owned; has experience gain
├── WildMonster           — catchable encounter monster
└── BossMonster           — multi-phase final boss; uncatchable

Move (abstract)
├── AttackMove            — damage via TypeChart multiplier
├── HealMove              — restores caster HP
└── StatusMove            — inflicts StatusCondition on target

Item (abstract)
├── CaptureOrb            — attempts to catch WildMonster
├── Potion                — restores HP by fixed amount
└── StatusCure            — clears a specific StatusCondition

Trainer (abstract)           <- Strategy Pattern: ChooseAction() is the strategy interface
├── PlayerTrainer         — returns UseMove; action driven by external input
└── AITrainer             — selects best type-advantage move from active monster's moveset

BattleAnimation (abstract)
└── (9 rendering subclasses: HitFlash, DamageNumber, HealFlash, HealNumber,
     HealParticle, StatusSwirl, CaptureOrb, PotionGlow, TurnSkip)
```

---

## 4. Composition and Aggregation

| Container | Holds | Relationship | Notes |
|---|---|---|---|
| `Trainer` | `Party` | Composition | created in `Trainer` constructor |
| `Trainer` | `Inventory` | Composition | created in `Trainer` constructor |
| `Party` | `List<TamedMonster>` | Aggregation | max 6 slots |
| `Inventory` | `List<Item>` | Aggregation | max 20 slots |
| `TamedMonster` | `Move[]` | Aggregation | moveset assigned at construction |
| `WildMonster` | `Move[]` | Aggregation | moveset assigned at construction |
| `BossMonster` | `Move[]` | Aggregation | moveset assigned at construction |
| `BattleManager` | `PlayerTrainer` | Association | passed by reference |
| `BattleManager` | `Trainer` | Association | opponent reference (base type) |
| `BattleManager` | `WildMonster?` | Association | set when battling a wild monster |
| `BattleManager` | `BossMonster?` | Association | set when battling the final boss |
| `BattleManager` | `List<LogEntry>` | Composition | owned combat log |
| `GameManager` | `PlayerTrainer` | Composition | created in constructor |
| `GameManager` | `BattleManager?` | Association | created per battle, discarded after |
| `GameManager` | `List<Region>` | Composition | all 3 regions |
| `GameManager` | `OverworldMap?` | Association | current map instance |
| `GameManager` | `List<BattleAnimation>` | Aggregation | active animations pruned each frame |
| `Region` | `List<WildMonster>` | Aggregation | encounter pool |
| `Region` | `List<AITrainer>` | Aggregation | NPC trainers in region |
| `OverworldMap` | `Tile[,]` | Composition | 25x20 grid |
| `OverworldMap` | `OverworldPlayer` | Composition | player state |
| `OverworldMap` | `List<TrainerNPC>` | Aggregation | NPC positions on map |
| `TrainerNPC` | `AITrainer` | Association | reference to trainer logic |
| `BattleState` | `Monster` x2 | Association | read-only snapshot references |

---

## 5. Important Dependencies Between Classes

| From | To | Reason |
|---|---|---|
| `AttackMove` | `TypeChart` | `GetMultiplier()` called inside `Execute()` |
| `AITrainer.ChooseAction()` | `BattleState` | reads `PlayerMonster` types to score moves |
| `AITrainer.ChooseAction()` | `TypeChart` | evaluates each move's effectiveness |
| `BattleManager.EnemyTurn()` | `Trainer.ChooseAction()` | Strategy Pattern dispatch |
| `BattleManager.EnemyTurn()` | `BattleState` | constructs snapshot for AI |
| `BattleManager` | `GameManager` (events) | fires `AttackHit`, `HealUsed`, `StatusApplied`, `TurnSkipped` |
| `CaptureOrb.AttemptCapture()` | `WildMonster.CatchRate` | probability formula |
| `Region.GetRandomEncounter()` | `MonsterFactory` | delegates creation of fresh instances |
| `GameManager.SetupGame()` | `MonsterFactory` | creates starter monster + initial items |
| `GameManager.StartBossBattle()` | `MonsterFactory.CreateNagaBelian()` | spawns final boss |
| `GameManager.LoadOverworldMap()` | `MapFactory` | builds region tile map |
| `GameManager` | `BattleManager` | creates, reads result, subscribes to events |
| `GameManager` | `OverworldMap` | polls trigger flags each update frame |
| `GameManager` | `Region` | queries encounters; marks cleared |

---

## 6. Responsibilities of Each Class

| Class | Responsibility |
|---|---|
| `Monster` | Core monster data (HP, level, type, status). Shared operations: TakeDamage, RestoreHp, LevelUp, ApplyStatusEffect. |
| `TamedMonster` | Adds XP tracking and level-up threshold. Holds moveset. Lives in the player party. |
| `WildMonster` | Adds catch rate. Encountered in tall grass. Converted to TamedMonster on capture. |
| `BossMonster` | Adds phase system (1-3 from HP%). Phase affects battle cry. Uncatchable. |
| `Move` | A named battle action with power and type. Declares abstract `Execute()` and `GetMoveCategory()`. |
| `AttackMove` | Type-multiplied damage via TypeChart. Deals damage to target. |
| `HealMove` | Restores HP to caster (power + level*2). |
| `StatusMove` | Inflicts a status condition on target; paralysis has 50% resistance chance. |
| `Item` | Named consumable with rarity. Declares abstract `Use()` and `GetDescription()`. |
| `CaptureOrb` | Computes catch probability from HP%. `AttemptCapture()` returns CaptureResult. |
| `Potion` | Heals a fixed HP amount on a TamedMonster. |
| `StatusCure` | Clears a specific status condition from a TamedMonster. |
| `Trainer` | Owns party and bag. Declares `ChooseAction(BattleState)` — the Strategy interface. |
| `PlayerTrainer` | Stores selected move index and gold. `ChooseAction()` is a stub; input is set externally by GameManager. |
| `AITrainer` | `ChooseAction()` loops moveset, picks index with highest TypeChart multiplier against opponent. |
| `BattleManager` | Turn execution engine. Runs player and enemy half-turns, applies status ticks, fires animation events, tracks BattleResult. Does not draw. |
| `BattleState` | Immutable snapshot (player monster, enemy monster, turn number) passed to AI for decision-making. |
| `GameManager` | Singleton application shell. Owns the game loop, routes input per GameState, transitions states, delegates battle to BattleManager, delegates map to OverworldMap, renders all screens. |
| `Party` | Fixed-capacity (6) ordered list of TamedMonster. `GetActiveMonster()` returns first alive. Supports `SwapToFront()`. |
| `Inventory` | Fixed-capacity (20) list of Item. Add/remove by reference. |
| `Region` | Holds encounter pool and trainer list for one map zone. Generates fresh instances via MonsterFactory. Tracks cleared state. |
| `TypeChart` | Static 8x8 float matrix. Returns damage multiplier for attack/defense type pair. |
| `MonsterFactory` | Static factory for all 8 tameables + final boss. `Tame()` converts WildMonster to TamedMonster. |
| `OverworldMap` | 25x20 tile grid. Player movement + collision, camera tracking, per-tile trigger detection (wild, trainer, exit, shop). Also renders the map. |
| `Tile` | Immutable tile: `TileType` and derived `Walkable` flag. |
| `OverworldPlayer` | Tracks position, facing direction, and walk animation frame. |
| `TrainerNPC` | NPC's map position, linked AITrainer, and defeated flag. |
| `MapFactory` | Static factory. Builds hardcoded tile grids for the 3 region maps. |
| `BattleAnimation` | Abstract animation node: frame counter, duration, `Update()` (concrete), `Draw()` (abstract). |
| `LogEntry` | Readonly struct: battle log text + LogEntryType tag for colour-coded display. |

---

## 7. Key Public Methods

### Monster
- `TakeDamage(int amount)` — clamps HP at 0
- `RestoreHp(int amount)` — clamps HP at MaxHp
- `LevelUp()` — increments level, raises MaxHp by 10
- `ApplyStatusEffect()` → `int` — burn (10% maxHp) or poison (8% maxHp) damage per turn
- `GetBattleCry()` → `string` *(abstract)*
- `GetDescription()` → `string` *(abstract)*

### TamedMonster
- `GainExperience(int amount)` — triggers `LevelUp()` when XP >= Level * 100

### BossMonster
- `UpdatePhase()` — sets Phase 1/2/3 at HP thresholds 66% / 33%

### CaptureOrb
- `ComputeCatchChance(WildMonster)` → `float`
- `AttemptCapture(WildMonster)` → `CaptureResult`

### Move
- `Execute(Monster caster, Monster target)` → `string` *(abstract)*
- `GetMoveCategory()` → `string` *(abstract)* — "Attack", "Heal", or "Status"

### Trainer
- `ChooseAction(BattleState state)` → `TrainerAction` *(abstract)*
- `GetActiveMonster()` → `TamedMonster?`

### BattleManager
- `PlayerTurn(int moveIndex)` → `string`
- `EnemyTurn()` → `string`
- `Flee()` — sets result to PlayerFled
- `AttemptCapture(CaptureOrb orb)` → `CaptureResult`
- `IsBattleOver(bool isEndOfRound)` → `bool`
- `AddLog(string, LogEntryType)`
- Events: `AttackHit`, `HealUsed`, `StatusApplied`, `TurnSkipped`, `StatusRemoved`

### GameManager
- `GetInstance()` → `GameManager` *(Singleton accessor)*
- `Run()` — game loop: ProcessEvents → HandleInput → Update → Draw
- `HandleInput()` — state-switched input routing
- `Update()` — pacing timers, overworld trigger polling, animation pruning
- `Draw()` — clears screen, dispatches to per-state draw methods

### Party
- `AddMonster(TamedMonster)` → `bool` — false if full
- `GetActiveMonster()` → `TamedMonster?`
- `SwapToFront(int index)` → `bool`
- `IsWiped()` → `bool`

### TypeChart
- `GetMultiplier(MonsterType attack, MonsterType defend)` → `float` (0.0 / 0.5 / 1.0 / 2.0)

### OverworldMap
- `Update(bool up, bool down, bool left, bool right)` — movement + trigger detection
- `Draw(Window, Dictionary<string,Bitmap>)` — full map + HUD render
- `MarkTrainerDefeated(AITrainer)` — hides NPC from map
- `ApplyFleeingCooldown()` — 300-frame encounter suppression
- Trigger flags: `WildEncounterTriggered`, `TrainerEncounterTriggered`, `RegionExitTriggered`, `ShopEncounterTriggered`

---

## 8. Core Gameplay Systems

### Battle System
`BattleManager` drives turn-based combat. Each round:
1. Player half-turn: status tick → skip check → `Move.Execute()` → animation events fire.
2. Enemy half-turn: status tick → `Trainer.ChooseAction(BattleState)` → `Move.Execute()` → animation events fire.
3. `IsBattleOver(isEndOfRound: true)` checks fainted monsters; advances turn counter; calls `GainExperience()` on win.

### Overworld Exploration
`OverworldMap` manages a 25x20 tile grid at 2px/frame movement with diagonal normalisation. Each frame `CheckTriggers()` evaluates the occupied tile:
- `TallGrass` → 15% per new tile → `WildEncounterTriggered`
- Proximity to TrainerNPC (< 1.5 tiles) → `TrainerEncounterTriggered`
- `RegionExit` → fade-to-black transition → `RegionExitTriggered`
- `Shop` → `ShopEncounterTriggered`

### Capture System
`CaptureOrb.AttemptCapture(wild)` formula: `(1 - hpPct) * catchRate * modifier`. On `Caught`, `MonsterFactory.Tame(wild)` creates a TamedMonster clone and `Party.AddMonster()` attempts to add it (fails silently if party is full — monster is released).

### Region Progression
Three sequential regions (Rainforest → River Delta → Ancient Highlands). All trainers in a region must be defeated before the EXIT tile activates. Stepping on EXIT triggers `AdvanceRegion()`; after the third region, `StartBossBattle()` spawns the final boss.

### Economy
`PlayerTrainer.Gold` gained on each battle win (50–100 random). Spent at Shop tiles on Potion, Super Potion, or Capture Orb via the shop catalogue in `GameManager`.

---

## 9. Design Patterns Detected

### Singleton — `GameManager`
```csharp
private static GameManager? _instance = null;
public static GameManager GetInstance() { ... }
```
One instance enforced via private constructor. Controls the single game window, loop, and player state.

### Factory — `MonsterFactory` and `MapFactory`
`MonsterFactory` (static) creates all 9 monster species. Callers never use `new TamedMonster/WildMonster/BossMonster` outside the factory. `MonsterFactory.Tame(wild)` converts WildMonster → TamedMonster. `MapFactory` (static) builds the tile grids for all three regions.

### Strategy — `Trainer.ChooseAction(BattleState)`
`Trainer` is the abstract context/interface. `PlayerTrainer` and `AITrainer` are concrete strategies. `BattleManager.EnemyTurn()` calls `_opponent.ChooseAction(state)` on the base type — the algorithm is fully swappable.

### Observer / Event — `BattleManager` Events
`BattleManager` publishes five C# events (`AttackHit`, `HealUsed`, `StatusApplied`, `TurnSkipped`, `StatusRemoved`). `GameManager` subscribes at battle start. Handlers instantiate the appropriate `BattleAnimation`. This decouples combat logic from visual effects.

### Template Method — `BattleAnimation`
`Update()` (base class, concrete) advances `_frame`. `Draw()` (abstract) is implemented by each subclass. Subclasses inherit lifecycle management and contribute only the drawing behaviour.

---

## 10. Candidate Workflows for Sequence Diagrams

### SD-1: Player Attack Turn
```
User presses Enter
-> GameManager.HandleBattleInput()
-> BattleManager.PlayerTurn(moveIndex)
     -> Monster.ApplyStatusEffect()          [burn/poison tick]
     -> Move.Execute(attacker, defender)
          -> TypeChart.GetMultiplier()        [AttackMove only]
          -> Monster.TakeDamage(damage)
     -> AttackHit event fires
-> GameManager.OnAttackHit()
     -> new HitFlashAnimation(cx, cy)
     -> new DamageNumberAnimation(cx, cy, damage)
-> BattleManager.IsBattleOver()
-> GameManager: _pendingEnemyTurn = true
```

### SD-2: AI Enemy Turn (after inter-turn delay)
```
GameManager.Update() [_interTurnDelay reaches 0]
-> BattleManager.EnemyTurn()
     -> new BattleState(defender, attacker, turn, false)
     -> AITrainer.ChooseAction(state)
          -> TypeChart.GetMultiplier() x moveset length
          -> returns TrainerAction.UseMove, stores _selectedMoveIndex
     -> Move.Execute(attacker, defender)
     -> Monster.TakeDamage()
     -> AttackHit event -> GameManager creates animations
-> BattleManager.IsBattleOver(isEndOfRound: true)
     -> TamedMonster.GainExperience()        [if PlayerWon]
```

### SD-3: Wild Encounter Trigger
```
GameManager.Update() [state == Overworld]
-> OverworldMap.Update(keys)
     -> TryMove() -> OverworldPlayer.Move()
     -> CheckTriggers() -> WildEncounterTriggered = true
<- GameManager detects WildEncounterTriggered
-> Region.GetRandomEncounter()
     -> MonsterFactory.Create*(randomLevel)
-> _pendingEncounter = wild; state = EncounterPrompt
User presses Enter
-> GameManager.StartWildBattle(wild)
     -> new BattleManager(player, dummyAI)
     -> BattleManager.WildTarget = wild
     -> subscribe to BattleManager events (+=)
     -> state = Battle
```

### SD-4: Capture Attempt
```
User selects CaptureOrb in item menu
-> GameManager.HandleItemInput()
-> Inventory.RemoveItem(orb)
-> BattleManager.AttemptCapture(orb)
     -> CaptureOrb.AttemptCapture(wildTarget)
          -> ComputeCatchChance() -> float
          -> Random.NextDouble() vs chance
          -> returns CaptureResult
-> new CaptureOrbAnimation(...)
-> if Caught:
     MonsterFactory.Tame(wild) -> TamedMonster
     Party.AddMonster(tamed) -> bool (false if full)
     _pendingCaptureEnd = true
-> [after captureEndTimer] state = CaptureScreen
```

### SD-5: Region Advancement
```
OverworldMap.CheckTriggers() detects RegionExit tile
-> _exitFading = true [fade begins over ~32 frames]
-> RegionExitTriggered = true [when alpha reaches 255]
<- GameManager.Update() detects RegionExitTriggered
-> GameManager.AdvanceRegion()
     -> Region.Clear() [current region]
     -> if regionIndex < 2:
          _currentRegionIndex++
          LoadOverworldMap(index)
               -> MapFactory.Create*Map(npcs)
               -> new OverworldMap(...)
     -> else:
          StartBossBattle()
               -> MonsterFactory.CreateNagaBelian()
               -> new BattleManager(player, bossTrainer)
               -> BattleManager.BossTarget = nagaBelian
               -> subscribe events; state = Battle
```

### SD-6: Game Startup
```
Program.Main()
-> GameManager.GetInstance()
     -> new GameManager()
          -> new Window(...)
          -> new PlayerTrainer("Ranger")
          -> SetupGame()
               -> MonsterFactory.CreateKelabit(level: 8)
               -> Party.AddMonster(starter)
               -> Bag.AddItem(Potion x2, CaptureOrb x3)
               -> SetupRegions()
                    -> new Region("Rainforest")
                         -> AddEncounter(Bekantan, Pelanduk, Rajah)
                         -> AddTrainer(AITrainer "Ranger Lili")
                    -> new Region("River Delta") ...
                    -> new Region("Ancient Highlands") ...
               -> LoadOverworldMap(0)
                    -> MapFactory.CreateRainforestMap(npcs)
-> GameManager.Run()  [game loop starts]
```
