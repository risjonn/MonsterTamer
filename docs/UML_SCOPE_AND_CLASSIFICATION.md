# MonsterTamer — UML Scope and Class Classification

> Derived from FINAL_REPORT_ANALYSIS.md.
> This file is the authoritative reference for all UML diagram generation tasks.
> Sections 3–5 are final decisions; do not override them without updating the justifications here.

---

## 1. Class Classification

### 1.1 Core Gameplay Architecture
The domain backbone — classes that define what the game *is*.

| Class | Justification |
|---|---|
| `Monster` | Abstract root of the entity model; OOP requirement |
| `TamedMonster` | Primary player-facing entity; carries XP, moveset, party slot |
| `WildMonster` | The encounter entity; catchable; bridges overworld and battle |
| `BossMonster` | Multi-phase final adversary; extends the Monster model meaningfully |
| `Move` | Abstract root of all combat actions |
| `AttackMove` | Most architecturally significant move — depends on TypeChart |
| `Item` | Abstract root of the consumable model |
| `CaptureOrb` | Most architecturally significant item — owns `AttemptCapture()` |
| `Trainer` | Abstract root of the actor model; Strategy pattern context |
| `BattleManager` | Core turn engine; owns events, log, result |
| `OverworldMap` | Core exploration system; owns tile grid and all trigger logic |
| `Region` | Links overworld zones to encounter pools and trainer rosters |
| `Party` | Core composition — Trainer's monster roster |

### 1.2 Supporting Gameplay Systems
Important but subordinate to the primary architecture.

| Class | Justification |
|---|---|
| `HealMove` | Same hierarchy level as `AttackMove`; adds healing axis |
| `StatusMove` | Same hierarchy level; adds status axis |
| `Potion` | Same hierarchy level as `CaptureOrb`; heal consumable |
| `StatusCure` | Same hierarchy level; status-clearing consumable |
| `Inventory` | Parallel to `Party`; Trainer's item bag |
| `BattleState` | Immutable DTO passed to `ChooseAction()` |
| `OverworldPlayer` | Position/animation state for the map player |
| `TrainerNPC` | NPC position, linked AITrainer, defeated flag |

### 1.3 Design Pattern Infrastructure
Classes whose primary architectural significance is as pattern exemplars.

| Class | Pattern | Justification |
|---|---|---|
| `GameManager` | Singleton | Owns the game loop; single instance enforced |
| `MonsterFactory` | Factory | Sole creator of all monster instances |
| `MapFactory` | Factory | Sole creator of tile grids |
| `PlayerTrainer` | Strategy (concrete) | Human-input strategy implementation |
| `AITrainer` | Strategy (concrete) | Type-advantage scoring strategy |
| `TypeChart` | *(shared data)* | Static lookup used by both `AttackMove` and `AITrainer` |

### 1.4 Animation / Presentation
Rendering-only; no behavioral role in the domain model.

`BattleAnimation` (abstract), `HitFlashAnimation`, `DamageNumberAnimation`, `HealFlashAnimation`,
`HealNumberAnimation`, `HealParticleAnimation`, `StatusSwirlAnimation`, `CaptureOrbAnimation`,
`PotionGlowAnimation`, `TurnSkipAnimation`

### 1.5 Utility / Helper
Leaf-level value types or data-only classes with no outward dependencies.

`LogEntry` (readonly struct), `Tile` (data-only), `BattleState` (behaviorless DTO)

---

## 2. Exploration System Analysis

### 2.1 The Four Classes Under Review

| Class | Role in exploration |
|---|---|
| `GameManager` | Router and state machine. Polls OverworldMap's trigger flags and responds (starts battles, advances regions). It is the *orchestrator*, not the *explorer*. |
| `BattleManager` | Handles what happens after an encounter begins. Entirely battle-scoped; zero responsibility for exploration. |
| `Region` | Content layer. Holds the encounter pool and trainer list for one zone. Defines *what* exists in a zone, not *how* the player navigates it. |
| `OverworldMap` | The exploration system itself. Owns the 25×20 tile grid, movement, collision, camera, and trigger detection. |

### 2.2 Is the Exploration System Sufficiently Represented Without OverworldMap?

**No.** With only GameManager, BattleManager, and Region in the diagram:

- The trigger mechanism — the key coupling point between exploration and battle — is invisible. GameManager appears to create battles from thin air.
- The architectural separation between *navigation* (`OverworldMap`) and *encounter content* (`Region`) disappears.
- Two of the six candidate sequence diagrams (SD-3: Wild Encounter, SD-5: Region Advancement) begin inside `OverworldMap.CheckTriggers()`. Without `OverworldMap` in the class diagram, those workflows are ungrounded — the reader cannot trace the pipeline start.
- `GameManager`'s dependency on `OverworldMap` (polling boolean trigger flags) is its most architecturally interesting dependency and must be visible to understand the system's event flow.

### 2.3 Should OverworldMap Appear as Full, Collapsed, or Omitted?

**Decision: Full class with selective members.**

| Option | Assessment |
|---|---|
| **Omit** | Breaks the exploration-to-battle pipeline narrative. GameManager loses its primary input source. Unacceptable. |
| **Collapsed** (name only) | Communicates existence but loses the trigger flag interface — the entire reason the dependency on `GameManager` exists. A box with no members cannot convey polling semantics. |
| **Full (selective)** | Show the trigger flags and behavioral entry points; omit all rendering members. This exposes the architectural interface without introducing rendering clutter. **Chosen.** |

**Members to show in the UML box:**

```
OverworldMap
─────────────────────────────────────
+ WildEncounterTriggered : bool
+ TrainerEncounterTriggered : bool
+ RegionExitTriggered : bool
+ ShopEncounterTriggered : bool
─────────────────────────────────────
+ Update(up, down, left, right : bool) : void
+ MarkTrainerDefeated(trainer : AITrainer) : void
+ ApplyFleeingCooldown() : void
```

All `Draw()` methods, camera state, tile internals, and `OverworldPlayer` details are omitted.

### 2.4 Justification from a University CS Report Perspective

1. **Encapsulation:** A 25×20 tile grid with collision and camera logic hidden behind a single `Update()` call is a textbook demonstration of encapsulation. Showing the class reinforces this.
2. **Coupling visibility:** The four boolean trigger flags are a deliberately chosen polling interface — `OverworldMap` writes them, `GameManager` reads and clears them. This design decision should be readable in the diagram without inspecting source code.
3. **Separation of concerns:** `Region` holds encounter content; `OverworldMap` handles spatial navigation. Both must appear together for a reader to understand the SoC boundary.
4. **Sequence diagram grounding:** A class diagram for a report that includes sequence diagrams should contain every lifeline that appears as a column header. `OverworldMap` is a lifeline in SD-3 and SD-5.
5. **Diagram count:** Moving from 16 to 17 classes remains well within the readable range for an A3 or full-page UML diagram.

---

## 3. Final Recommended Class List — Main Class Diagram

**Target: 17 classes.** All four OOP pillars and all three required design patterns are traceable.

| # | Class | Stereotype | Key members to show | Rationale |
|---|---|---|---|---|
| 1 | `Monster` | `«abstract»` | _name, _hp, _maxHp, _level, _primaryType, _statusCondition; TakeDamage(), RestoreHp(), LevelUp(), ApplyStatusEffect(), GetBattleCry()«abstract», GetDescription()«abstract» | Abstract root; OOP requirement; all shared entity state lives here |
| 2 | `TamedMonster` | | _experience, MoveSet[]; GainExperience() | Adds XP and moveset; is the player's monster type |
| 3 | `WildMonster` | | CatchRate; MoveSet[] | Adds catch rate; target of CaptureOrb |
| 4 | `BossMonster` | | Phase, TotalPhases; UpdatePhase() | Adds phase system; shows inheritance depth |
| 5 | `Move` | `«abstract»` | _name, _power, _moveType; Execute()«abstract», GetMoveCategory()«abstract» | Abstract root; Execute() is the primary polymorphism site |
| 6 | `AttackMove` | | Execute() — TypeChart-multiplied damage | Representative concrete move; key dependency on TypeChart |
| 7 | `Item` | `«abstract»` | _name, _rarity; Use()«abstract», GetDescription()«abstract» | Abstract root; Use() is a second polymorphism site |
| 8 | `CaptureOrb` | | _captureModifier; ComputeCatchChance(), AttemptCapture()→CaptureResult | Architecturally richest item; drives the capture system |
| 9 | `Trainer` | `«abstract»` | _party : Party, _inventory : Inventory; ChooseAction()«abstract», GetActiveMonster() | Strategy context; composes Party and Inventory |
| 10 | `PlayerTrainer` | | _gold, _selectedMoveIndex; ChooseAction() (stub), AddGold(), SpendGold() | Concrete strategy — human input path |
| 11 | `AITrainer` | | _selectedMoveIndex; ChooseAction() (type-scoring loop) | Concrete strategy — algorithmic path |
| 12 | `Party` | | _monsters : List\<TamedMonster\>, MaxSlots=6; GetActiveMonster(), AddMonster(), SwapToFront(), IsWiped() | Composition target of Trainer; GetActiveMonster() is the active-slot contract |
| 13 | `BattleManager` | | PlayerTurn(), EnemyTurn(), Flee(), AttemptCapture(), IsBattleOver(); events: AttackHit, HealUsed, StatusApplied | Core turn engine; fires animation events; holds BattleResult |
| 14 | `GameManager` | `«singleton»` | _instance «static»; GetInstance()«static», Run(), HandleInput(), Update(), Draw() | Singleton orchestrator; owns the game loop and all top-level state |
| 15 | `Region` | | _encounters : List\<WildMonster\>, _trainers : List\<AITrainer\>; GetRandomEncounter(), Clear() | Content layer for one exploration zone; depends on MonsterFactory |
| 16 | `TypeChart` | `«static»` | GetMultiplier(attack, defend : MonsterType)→float | Shared data dependency of AttackMove and AITrainer; must be visible |
| 17 | `OverworldMap` | | WildEncounterTriggered, TrainerEncounterTriggered, RegionExitTriggered, ShopEncounterTriggered; Update(), MarkTrainerDefeated(), ApplyFleeingCooldown() | Exploration system source; polling interface with GameManager; required for SD-3 and SD-5 |

**Sibling omission note** (annotate as a diagram note):
- `HealMove`, `StatusMove` omitted — same hierarchy level as `AttackMove`; exist but are architecturally redundant in the diagram.
- `Potion`, `StatusCure` omitted — same hierarchy level as `CaptureOrb`.
- `Inventory` omitted — parallel composition to `Party`; same pattern, no new information.

---

## 4. Final Recommended Class List — Design Pattern Diagram

Present as three named panels in one diagram, or three separate diagrams. Only show members relevant to demonstrating the pattern.

### Panel A — Singleton: GameManager

```
GameManager
─────────────────────────────────────
- _instance : GameManager  «static»
─────────────────────────────────────
- GameManager()            «private constructor»
+ GetInstance() : GameManager  «static»
```

Arrow: `«access»` from a client note (e.g., "Program.Main()") to `GetInstance()`.

### Panel B — Factory: MonsterFactory + MapFactory

```
MonsterFactory «static»             MapFactory «static»
─────────────────────────────────── ───────────────────────────────────
+ CreateKelabit(level) : TamedMonster   + CreateRainforestMap(...) : OverworldMap
+ Create*(level) : WildMonster / TamedMonster / BossMonster
+ Tame(wild) : TamedMonster
```

Arrows: `«creates»` from `MonsterFactory` → `TamedMonster`, `WildMonster`, `BossMonster`. `«creates»` from `MapFactory` → `OverworldMap`. Do not show product class internals.

### Panel C — Strategy: Trainer / ChooseAction

```
BattleManager ──calls──> Trainer «abstract»
                              ChooseAction(state: BattleState): TrainerAction
                             /                        \
               PlayerTrainer                      AITrainer
               ChooseAction() → UseMove (stub)    ChooseAction() → type-scoring loop
```

Members to show: only `ChooseAction(BattleState): TrainerAction` on `Trainer`; its override on each concrete class. Include `BattleState` as a parameter-type note (not a full class box).

### Observer / Event and Template Method

Do not draw separate panels for these unless the report specification requires it. Reference them in prose using the following summary:

| Pattern | Where | Evidence |
|---|---|---|
| Observer / Event | `BattleManager` → `GameManager` | Five C# events (`AttackHit`, `HealUsed`, `StatusApplied`, `TurnSkipped`, `StatusRemoved`); GameManager subscribes at battle start |
| Template Method | `BattleAnimation` | `Update()` (concrete base) advances frame counter; `Draw()` (abstract) implemented by 9 subclasses |

---

## 5. Final Omission List

All classes below are excluded from both the main class diagram and the design pattern diagram.

| Class | Category | Reason for omission |
|---|---|---|
| `HealMove` | Supporting | Same hierarchy position as `AttackMove`; one concrete Move representative is sufficient |
| `StatusMove` | Supporting | Same hierarchy position as `AttackMove`; one concrete Move representative is sufficient |
| `Potion` | Supporting | Same hierarchy position as `CaptureOrb`; one concrete Item representative is sufficient |
| `StatusCure` | Supporting | Same hierarchy position as `CaptureOrb`; one concrete Item representative is sufficient |
| `Inventory` | Supporting | Parallel composition to `Party` (same pattern, same container role); `Party` is sufficient |
| `BattleState` | Utility | Behaviorless DTO; no methods, no inheritance; appears only as a parameter type in the Strategy panel |
| `LogEntry` | Utility | Readonly struct; pure data; no relationships or behavioral methods |
| `Tile` | Utility | Data-only immutable record; no outward dependencies |
| `OverworldPlayer` | Supporting | Tracks position and animation frame; rendering helper with no domain behavior |
| `TrainerNPC` | Supporting | Thin positional wrapper around `AITrainer`; no independent behavior |
| `MapFactory` | Design Pattern | Architecturally parallel to `MonsterFactory`; mentioned in Panel B of the design pattern diagram but does not need a standalone class entry in the main diagram |
| `BattleAnimation` | Animation | Abstract animation base; rendering-only lifecycle; no domain role |
| `HitFlashAnimation` | Animation | Rendering subclass; no domain role |
| `DamageNumberAnimation` | Animation | Rendering subclass; no domain role |
| `HealFlashAnimation` | Animation | Rendering subclass; no domain role |
| `HealNumberAnimation` | Animation | Rendering subclass; no domain role |
| `HealParticleAnimation` | Animation | Rendering subclass; no domain role |
| `StatusSwirlAnimation` | Animation | Rendering subclass; no domain role |
| `CaptureOrbAnimation` | Animation | Rendering subclass; no domain role |
| `PotionGlowAnimation` | Animation | Rendering subclass; no domain role |
| `TurnSkipAnimation` | Animation | Rendering subclass; no domain role |
