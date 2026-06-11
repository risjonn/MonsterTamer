# GameManager — UML Representation Guide

> Derived from a full read of `src/GameManager.cs` (2052 lines).
> Purpose: determine exactly which members to include, which to omit, and how to represent
> GameManager in both the main class diagram and the design pattern diagram.

---

## 1. Architectural Responsibilities

GameManager has four distinct and separable responsibilities. Understanding them prevents
conflating rendering code with domain logic when deciding what belongs in a UML diagram.

### 1.1 Singleton Identity and Application Lifecycle
GameManager is the entry point and sole application shell. A private static field and private
constructor enforce a single instance. `Run()` owns the SplashKit window and the main game loop.
Nothing outside GameManager can start or stop the game.

### 1.2 State Machine
GameManager drives an eight-state finite state machine (`GameState` enum). `HandleInput()` and
`Update()` both switch on `_state` to route behavior. `Draw()` switches on `_state` to route
rendering. State transitions happen inside methods like `StartWildBattle()`, `EndBattle()`, and
`AdvanceRegion()`. The state machine is the core of how GameManager coordinates all other systems.

### 1.3 Battle Lifecycle Management
GameManager creates and destroys `BattleManager` instances. It subscribes event handlers to
`BattleManager`'s five events at the start of each battle and holds the reference for the
duration. After a battle ends (`EndBattle()`), it processes the result: awards gold, marks
the NPC defeated on the map, checks whether all region trainers are cleared, and transitions
the state machine.

### 1.4 Overworld and Region Progression
GameManager polls `OverworldMap` trigger flags on each `Update()` frame and responds:
wild triggers start wild battles, trainer triggers start trainer battles, exit triggers call
`AdvanceRegion()`. `AdvanceRegion()` either loads the next `Region` map (via `MapFactory`) or
spawns the final boss when all three regions are cleared.

---

## 2. Architecturally Significant Fields

These fields should appear in the UML class diagram. Each one either represents a direct
composition/aggregation relationship or drives the state machine.

| Field | Type | Why architecturally significant |
|---|---|---|
| `_instance` | `static GameManager?` | The Singleton field — the single instance that `GetInstance()` guards and returns. Must appear in the UML to make the Singleton pattern legible. |
| `_state` | `GameState` | The state machine variable. Every input routing, update tick, and draw call is gated on this field. Omitting it makes the class appear to have one execution path. |
| `_player` | `PlayerTrainer` | Composition relationship — GameManager creates and owns the player. This is the composition arrow target in the UML. |
| `_battleManager` | `BattleManager?` | Association — created per battle, discarded after. Nullable reflects its transient existence. Justifies the association arrow to BattleManager. |
| `_regions` | `List<Region>` | Composition — GameManager creates all three `Region` objects in `SetupRegions()` and owns them for the session. Justifies the composition arrow to Region. |
| `_currentRegionIndex` | `int` | Tracks linear progression through the three regions. Together with `_regions`, makes `AdvanceRegion()` comprehensible. |
| `_overworldMap` | `OverworldMap?` | Association — current map instance, replaced on region advance. Justifies the association arrow to OverworldMap and the polling relationship. |

---

## 3. Architecturally Significant Methods

These methods should appear in the UML class diagram. Each one represents one of the four
architectural responsibilities identified in Section 1.

| Method | Visibility | Why architecturally significant |
|---|---|---|
| `GetInstance() : GameManager` | `public static` | **Required for Singleton.** The only legal construction path. Must appear in both the main diagram and the design pattern diagram. |
| `Run() : void` | `public` | The game loop: ProcessEvents → HandleInput → Update → Draw. It is the top-level entry point and the only caller of the other three loop methods. |
| `HandleInput() : void` | `public` | State-switched input router. Delegates to `StartWildBattle()`, `StartTrainerBattle()`, etc. Represents the input subsystem at the architectural level. |
| `Update() : void` | `public` | Pacing and trigger polling: advances animation pruning, counts inter-turn delay, polls `OverworldMap` flags, calls `AdvanceRegion()`. Represents the update subsystem. |
| `Draw() : void` | `public` | State-switched render dispatch. Delegates to per-state draw methods. Represents the rendering entry point without exposing any rendering details. |
| `StartWildBattle(wild : WildMonster)` | `private` | Creates `BattleManager`, sets `WildTarget`, subscribes four event handlers, transitions state to `Battle`. Shows the Observer/Event pattern setup. |
| `StartTrainerBattle(trainer : AITrainer)` | `private` | Creates `BattleManager`, subscribes event handlers, transitions state. Parallel to `StartWildBattle`. |
| `StartBossBattle()` | `private` | Creates `BossMonster` via `MonsterFactory`, creates `BattleManager`, sets `BossTarget`, subscribes events. Final boss pathway. |
| `EndBattle()` | `private` | Post-battle outcome processing: awards gold, marks NPC defeated, checks region-clear condition, transitions state to Overworld or GameOver. |
| `AdvanceRegion()` | `private` | Clears current region, increments index, calls `LoadOverworldMap()` (→ `MapFactory`), or triggers `StartBossBattle()` when index exceeds region count. |
| `SetupGame()` | `private` | Calls `MonsterFactory.CreateKelabit()` for the starter, populates the bag, calls `SetupRegions()` and `LoadOverworldMap(0)`. Demonstrates Factory pattern usage at startup. |

---

## 4. Members to Omit

### 4.1 Rendering — Omit All

All `Draw*` methods and their supporting fields contain only SplashKit draw calls. They carry
no domain logic and no inter-class relationships. Adding them to the UML introduces 15+ methods
that communicate nothing architectural.

| Method | Reason |
|---|---|
| `DrawMainMenu()` | SplashKit geometry and text only |
| `DrawOverworld()` | Delegates to `OverworldMap.Draw()` + gold badge overlay |
| `DrawBattle()` | HP bars, sprites, move panel, log — all rendering |
| `DrawEncounterPrompt()` | Encounter name card overlay |
| `DrawPartyScreen()` | Party slot grid rendering |
| `DrawCaptureScreen()` | Capture result card rendering |
| `DrawShop()` | Shop menu overlay |
| `DrawVictory()` | Victory screen text |
| `DrawGameOver()` | Game over screen text |
| `DrawMonsterSprite()` | Geometric shape drawing per monster |
| `DrawInfoPanel()` | HP/level/type panel rendering |
| `DrawTypeBadge()` | Type chip rendering |
| `DrawHpBar()` | Animated HP bar rendering |
| `DrawMovePanel()` | Move grid rendering |
| `DrawBattleLog()` | Scrolling log rendering |

| Field | Reason |
|---|---|
| `_monsterSprites` | Sprite bitmap cache — asset loading only |
| `_battleAnimations` | Animation node list — rendering loop only |
| `_wrappedLogCache`, `_wrappedLogCount` | Log text wrap cache — rendering performance |
| `_battleAnimFrame` | Frame counter for battle entrance animation |
| `_playerHpDisplay`, `_enemyHpDisplay` | Smoothed HP display values — visual interpolation |
| `_battleResultMessage` | Result string for the post-battle overlay |
| `ScreenW`, `ScreenH` | Screen resolution constants — layout only |

### 4.2 Animation — Omit

| Member | Reason |
|---|---|
| `OnAttackHit()` | Event handler that spawns `HitFlashAnimation` and `DamageNumberAnimation` at hardcoded screen coordinates |
| `OnHealUsed()` | Event handler that spawns `HealFlashAnimation`, `HealNumberAnimation`, `HealParticleAnimation` |
| `OnStatusApplied()` | Event handler that spawns `StatusSwirlAnimation` |
| `OnTurnSkipped()` | Event handler that spawns `TurnSkipAnimation` |
| `ResetBattleAnimState()` | Resets all animation and pacing fields to defaults |

These handlers are the subscriber side of the Observer pattern. They are worth mentioning in
prose to explain the Observer/Event pattern, but they belong to the animation layer and should
not appear in the main class diagram.

### 4.3 Input Handling — Omit (Internal Detail)

| Method | Reason |
|---|---|
| `HandleBattleInput()` | Sub-method of `HandleInput()`; move selection, C/X key routing |
| `HandleItemInput()` | Sub-method; item menu navigation and item use |
| `HandleSwapInput()` | Sub-method; party swap menu navigation |
| `HandleShopInput()` | Sub-method; shop catalogue navigation |

`HandleInput()` already represents the input subsystem at the UML level. Its sub-methods are
implementation decomposition, not separate architectural concerns.

### 4.4 Timing and Pacing — Omit

| Field | Reason |
|---|---|
| `_pendingEnemyTurn` | Boolean flag delaying enemy turn until `_interTurnDelay` reaches zero |
| `_interTurnDelay` | Frame countdown between player act and enemy act |
| `_pendingBattleEnd` | Flag set when result is determined but animations are still playing |
| `_battleResultTimer` | Countdown before auto-transitioning out of battle |
| `_pendingCaptureEnd` | Flag delaying capture-screen transition |
| `_captureEndTimer` | Frame countdown for capture animation |

These are internal pacing details with no inter-class relationships.

### 4.5 UI State — Omit

| Field | Reason |
|---|---|
| `_showingItemMenu`, `_selectedItemIndex`, `_bagScrollOffset` | Item menu open/selection state |
| `_showingSwapMenu`, `_selectedSwapIndex` | Swap menu open/selection state |
| `_showingShop`, `_selectedShopIndex` | Shop overlay state |
| `_pendingEncounter`, `_pendingTrainerEncounter`, `_lastTrainerBattled` | Encounter transition state (hold wild/trainer reference between trigger and battle start) |

### 4.6 Utility and Audio — Omit

| Member | Reason |
|---|---|
| `_rng` | Random number generator — utility |
| `_shopCatalog` | Inline data array defining shop item names, costs, and factories |
| `_overworldMusic`, `_battleMusic`, `_bgmReady`, `_activeBgm` | BGM state — audio |
| `LoadMonsterSprites()` | Asset loading — runs once in `Run()` |
| `LoadBgm()`, `PlayBgm()`, `StopBgm()` | Audio management |
| `SetupRegions()` | Sub-method of `SetupGame()`; if `SetupGame()` is shown, this is redundant |
| `LoadOverworldMap()` | Sub-method of `SetupGame()`/`AdvanceRegion()`; same reasoning |
| `ShowBattleResult()` | Internal helper: sets `_battleResultMessage` and `_pendingBattleEnd` |

### 4.7 Capture Result Screen Data — Omit

| Fields | Reason |
|---|---|
| `_capturedMonsterName`, `_capturedMonsterLevel`, `_capturedMonsterPrimary`, `_capturedMonsterSecondary`, `_capturedHasDual`, `_capturedHpAtCapture`, `_capturedMaxHp`, `_captureChanceLabel`, `_capturePartyMessage` | Snapshot fields for the `DrawCaptureScreen()` method. These have no behavioral role; they carry display data between the item-use event and the next Draw() call. |

---

## 5. Singleton Representation

### 5.1 Main Class Diagram

The Singleton pattern must be identifiable from the class box alone. The required representation is:

- `_instance` field must be shown with underline (static) and the correct type (`GameManager?`)
- The constructor must be shown as `- GameManager()` (private visibility, underscored or with a «private» note)
- `GetInstance()` must be shown underlined (static) with return type `GameManager`
- A UML note box attached to the class reading `«Singleton»` or `{only one instance}` is
  recommended for a university report to make the pattern explicitly labelled

**Members required in the main class diagram:**

```
GameManager
─────────────────────────────────────────────────────────────
- _instance : GameManager  «static»
- _state : GameState
- _player : PlayerTrainer
- _battleManager : BattleManager?
- _regions : List<Region>
- _currentRegionIndex : int
- _overworldMap : OverworldMap?
─────────────────────────────────────────────────────────────
- GameManager()     «private constructor»
+ GetInstance() : GameManager    «static»
+ Run() : void
+ HandleInput() : void
+ Update() : void
+ Draw() : void
- StartWildBattle(wild : WildMonster) : void
- StartTrainerBattle(trainer : AITrainer) : void
- StartBossBattle() : void
- EndBattle() : void
- AdvanceRegion() : void
- SetupGame() : void
```

### 5.2 Design Pattern Diagram

The design pattern diagram isolates the Singleton structure. Show only the members that
demonstrate the pattern; omit all domain state.

**Required members for the Singleton panel:**

```
GameManager
─────────────────────────────────────────────────────────────
- _instance : GameManager    «static»
─────────────────────────────────────────────────────────────
- GameManager()              «private»
+ GetInstance() : GameManager    «static»
```

Attach a note: `Returns _instance. Creates it on first call.`

**Optional (for context):** include `Run() : void` with a dashed arrow from a `«client»` note
labelled `Program.Main()` → `GameManager.GetInstance()` → `GameManager.Run()` to show the
call chain from the entry point.

**Members to hide in the Singleton panel:** everything in the domain state group (fields 2–7
from the table above) and all private methods. The Singleton panel must be readable in isolation
and communicate only the construction pattern.

---

## 6. Recommended UML Representation

The following is the final UML-ready class box for GameManager. Use this verbatim in the
main class diagram. All rendering, timing, UI, audio, and pacing members are excluded.

```
«singleton»
GameManager
═══════════════════════════════════════════════════════════════
- _instance       : GameManager?     {static}
──────────────────────────────────────────────────────────────
- _state          : GameState
- _player         : PlayerTrainer
- _battleManager  : BattleManager?
- _regions        : List<Region>
- _currentRegionIndex : int
- _overworldMap   : OverworldMap?
═══════════════════════════════════════════════════════════════
+ GetInstance() : GameManager        {static}
+ Run() : void
+ HandleInput() : void
+ Update() : void
+ Draw() : void
──────────────────────────────────────────────────────────────
- GameManager()                      «private»
- SetupGame() : void
- StartWildBattle(wild : WildMonster) : void
- StartTrainerBattle(trainer : AITrainer) : void
- StartBossBattle() : void
- EndBattle() : void
- AdvanceRegion() : void
```

### Relationship arrows from GameManager in the main diagram

| Arrow type | Target | Label | Justification |
|---|---|---|---|
| Composition `────◆` | `PlayerTrainer` | owns | Created in constructor; owned for entire session |
| Composition `────◆` | `List<Region>` | owns (3) | Created in `SetupRegions()`; owned for entire session |
| Association `────>` | `BattleManager?` | creates per battle | Nullable; lifespan is one battle only |
| Association `────>` | `OverworldMap?` | loads per region | Nullable; replaced on region advance |
| Dependency `- - ->` | `MonsterFactory` | `«uses»` | Calls `CreateKelabit()`, `CreateNagaBelian()` |
| Dependency `- - ->` | `MapFactory` | `«uses»` | Calls `CreateRainforestMap()` etc. via `LoadOverworldMap()` |

### Note on private methods in UML

University UML convention permits private methods in a class box when they represent a named
responsibility that justifies a relationship arrow or a pattern label. The six private methods
above (`SetupGame`, `StartWild/TrainerBattle`, `StartBossBattle`, `EndBattle`, `AdvanceRegion`)
each correspond to one of the architectural responsibilities in Section 1 and each directly
invokes another class in the diagram. They are not implementation detail — they are the
structural joints of the system.

The four `HandleBattle/Item/Swap/Shop Input()` sub-methods, all `Draw*()` methods, and all
event handlers are pure implementation detail and must not appear.
