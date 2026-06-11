# MonsterTamer — Architecture Delta: Design vs. Final Implementation

> Compares `docs/ARCHITECTURE_AND_UML.md` and `docs/PROJECT_SPECIFICATIONS.md` (original design)
> against the final implementation in `src/`.
> Use this document as the primary source for report Sections 5 and 8.

---

## 1. Major Architectural Changes

### 1.1 Battle Log: `List<string>` → `List<LogEntry>`

| | Detail |
|---|---|
| **Original design** | `BattleManager._battleLog : List<string>` |
| **Final implementation** | `BattleManager._battleLog : List<LogEntry>` where `LogEntry` is a `readonly struct` holding `Text : string` and `Type : LogEntryType` |
| **Reason** | The battle log needed semantic tagging to support color-coded rendering. A raw string list cannot carry the entry category (player move, enemy move, status tick, capture result, etc.) without re-parsing text at draw time. Introducing a typed struct separates data from presentation without coupling BattleManager to drawing code. |

---

### 1.2 BattleManager Gained an Event System (Observer Pattern)

| | Detail |
|---|---|
| **Original design** | No events. BattleManager exposed results only via return strings and properties. |
| **Final implementation** | Six C# events on BattleManager: `AttackHit(Monster, int)`, `HealUsed(Monster, int)`, `StatusApplied(Monster, StatusCondition)`, `StatusDamage(Monster, StatusCondition, int)`, `TurnSkipped(Monster, StatusCondition)`, `StatusRemoved(Monster, StatusCondition)`. GameManager subscribes at battle start and spawns the appropriate `BattleAnimation` on each event. |
| **Reason** | Rendering and audio feedback for individual combat actions require knowing exactly when and what happened inside BattleManager. Polling state every frame would miss short-lived events. C# events decouple BattleManager from GameManager: BattleManager never references a renderer; GameManager never inspects internal combat state to infer what animation to play. |

---

### 1.3 `Monster.ApplyStatusEffect()` Return Type: `void` → `int`

| | Detail |
|---|---|
| **Original design** | `ApplyStatusEffect() : void` |
| **Final implementation** | `ApplyStatusEffect() : int` — returns the HP damage dealt (0 for non-damaging statuses) |
| **Reason** | The event system needs the exact damage value at the moment of the status tick so it can be displayed in the battle log and passed to `StatusDamage` animation handlers. Recomputing the value outside Monster would duplicate the formula. Returning it directly from the method is the encapsulation-correct solution. |

---

### 1.4 `BattleManager.IsBattleOver()` Gained a Parameter

| | Detail |
|---|---|
| **Original design** | `IsBattleOver() : bool` |
| **Final implementation** | `IsBattleOver(bool isEndOfRound) : bool` |
| **Reason** | Experience gain (`TamedMonster.GainExperience()`) must trigger exactly once per round, not once per half-turn. The `isEndOfRound` flag gates the XP call to the end of the enemy half-turn only. Without this parameter, PlayerTurn and EnemyTurn both calling `IsBattleOver()` would double-award XP or award it mid-round. |

---

### 1.5 BattleManager Gained a `BossMonster` Target

| | Detail |
|---|---|
| **Original design** | `BattleManager._wildTarget : WildMonster?` only. Opponent was always a Trainer. |
| **Final implementation** | Added `BattleManager._bossTarget : BossMonster?` and the computed property `EnemyMonster : Monster?` which resolves `_wildTarget ?? _bossTarget ?? _opponent.GetActiveMonster()`. |
| **Reason** | The boss fight is structurally different from a trainer battle: the boss is a single BossMonster (not a Trainer with a Party), and capture is disallowed. Separating the three battle modes into distinct nullable fields on BattleManager avoided conditional branching in GameManager and kept `EnemyMonster` as the single resolution point for any subsequent battle logic. |

---

### 1.6 `GameState` Enum Expanded: 6 → 8 States

| | Detail |
|---|---|
| **Original design** | `GameState`: MainMenu, Overworld, Battle, PartyScreen, Victory, GameOver (6 values) |
| **Final implementation** | Added `EncounterPrompt` and `CaptureScreen` (8 values) |
| **Reason** | `EncounterPrompt` is a transitional state between overworld trigger detection and battle start, giving the player a "You encountered X" screen before committing to battle. `CaptureScreen` is a post-capture result state that shows whether the capture succeeded and whether the monster joined the party. Both prevent abrupt state transitions and require independent draw and input routing in GameManager. |

---

### 1.7 `Region.GetRandomEncounter()` Upgraded to Factory Delegation

| | Detail |
|---|---|
| **Original design** | Region held a `List<WildMonster>` encounter table; `GetRandomEncounter()` returned a reference from that list. |
| **Final implementation** | `GetRandomEncounter()` calls `MonsterFactory.Create*()` with a level value of `baseLevel ± 2` (random variance) to produce a fresh instance each time. |
| **Reason** | Returning a shared reference from a fixed list means every battle modifies the same WildMonster object (HP, status). Creating a fresh instance per encounter eliminates all shared-state bugs and allows level variance for encounter difficulty scaling without storing multiple pre-built instances. |

---

### 1.8 `Monster` Gained `AttackMultiplier` Property

| | Detail |
|---|---|
| **Original design** | Burn status was only described as a per-turn HP tick. No mention of an attack penalty. |
| **Final implementation** | `Monster.AttackMultiplier : float` returns `0.75f` when Burned, `1.0f` otherwise. Applied inside `AttackMove.Execute()` before damage calculation. |
| **Reason** | The Burned status condition is more mechanically meaningful if it also weakens the afflicted monster's attacks, matching the genre expectation. Adding it as a property on Monster keeps the check encapsulated and avoids duplicating the burn condition check in every move that deals damage. |

---

### 1.9 `OverworldMap` Gained Three New Members

| | Detail |
|---|---|
| **Original design** | `OverworldMap` had: `WildEncounterTriggered`, `TrainerEncounterTriggered`, `RegionExitTriggered`, `Player`, `Draw(Window)` |
| **Final implementation** | Added: `ShopEncounterTriggered : bool`, `MarkTrainerDefeated(AITrainer) : void`, `ApplyFleeingCooldown() : void` |
| **Reason** | Shop tiles were added as a fourth trigger type for the economy system. `MarkTrainerDefeated()` is called by GameManager after a trainer battle ends to hide the NPC from the map and unlock region progression. `ApplyFleeingCooldown()` suppresses wild encounter checks for 300 frames after the player flees, preventing immediate re-encounters. |

---

## 2. New Classes Added

| Class | Category | Purpose |
|---|---|---|
| `LogEntry` | `readonly struct` | Pairs a battle log string with a `LogEntryType` semantic tag. Consumed by the battle log renderer to apply per-category color coding. Replaces raw `List<string>`. |
| `BattleAnimation` | Abstract class | Abstract animation node managing frame counter and duration. Declares `Draw()` as abstract; concrete subclasses contribute only rendering behavior (Template Method pattern). |
| `HitFlashAnimation` | Animation | Renders a brief colored flash over the target monster position on a successful attack hit. |
| `DamageNumberAnimation` | Animation | Floats the damage number upward from the target's position over several frames. |
| `HealFlashAnimation` | Animation | Renders a green glow over a monster when HP is restored. |
| `HealNumberAnimation` | Animation | Floats the heal amount upward, color-coded green. |
| `HealParticleAnimation` | Animation | Rising particle effect accompanying a heal event. |
| `StatusSwirlAnimation` | Animation | Swirl effect shown when a status condition is applied or removed. |
| `CaptureOrbAnimation` | Animation | Orb throw arc and shake sequence played during a capture attempt. |
| `PotionGlowAnimation` | Animation | Bottle visual played when a Potion item is used. |
| `TurnSkipAnimation` | Animation | Overlaid indicator shown when a monster's turn is skipped due to Frozen or Paralysed. |

---

## 3. Classes Removed or Consolidated

### 3.1 `BattleManager.ClearLog()` — Dropped

The original spec listed `ClearLog() : void` on BattleManager. The final implementation does not include it. The battle log is append-only throughout the battle. Clearing it between turns proved unnecessary because the display scrolls through the full log; partial clearing would discard earlier context visible to the player.

### 3.2 `Region.GetNextTrainer()` — Dropped

The original spec listed `GetNextTrainer() : AITrainer?` on Region. In the final implementation, trainer state (which trainers are available, which are defeated) is tracked via `TrainerNPC` objects on `OverworldMap`, not through a linear queue on Region. GameManager calls `MarkTrainerDefeated(aiTrainer)` on the map after a battle ends. The sequential queue approach was replaced by spatial proximity triggers on the map, which is more accurate to the overworld exploration model.

---

## 4. New Gameplay Systems

### 4.1 Economy System
`PlayerTrainer` gained `_gold : int` (starting value: 50), `AddGold(int) : void`, and `SpendGold(int) : bool`. Gold is awarded after each battle win (50–100 range). Shop tiles on the overworld map trigger a `ShopEncounterTriggered` flag that GameManager detects, displaying a purchase menu for Potion, Super Potion, and Capture Orb. The economy system creates a resource loop between battling and exploration.

### 4.2 Battle Animation System
Nine concrete `BattleAnimation` subclasses are spawned by GameManager in response to BattleManager events. Each animation is added to `GameManager._activeAnimations` and pruned from the list when `IsComplete` is true. This system decouples visual feedback entirely from combat logic; BattleManager raises an event and never concerns itself with how the event is displayed.

### 4.3 Encounter Prompt Screen
A new `EncounterPrompt` game state displays the encountered monster's name, type, and level before the player confirms or flees. This state is entered when `OverworldMap.WildEncounterTriggered` or `TrainerEncounterTriggered` is detected, and exited when the player presses Enter (enter battle) or Escape (return to overworld).

### 4.4 Capture Result Screen
A new `CaptureScreen` game state shows the CaptureOrb animation sequence to completion and then displays the capture result (Caught / Escaped / Failed) along with a party-full warning if the monster could not be added. The state transitions back to Overworld after a timed delay.

### 4.5 Flee Mechanic with Encounter Cooldown
`BattleManager.Flee()` sets the result to `PlayerFled` and exits the battle. After fleeing, `OverworldMap.ApplyFleeingCooldown()` is called, which suppresses wild encounter trigger checks for 300 frames. Without this, the player re-enters the same encounter immediately on the next movement step.

### 4.6 In-Battle Monster Swapping
`Party.SwapToFront(int index) : bool` was added to support swapping the active monster during battle. `PlayerTrainer._selectedPartyIndex` tracks the currently highlighted party slot in the party selection screen. The `SwapMonster` `TrainerAction` enum value, which existed in the original design but was unimplemented, is now wired through the game state machine.

---

## 5. Design Pattern Evolution

### Planned (original design document)

| Pattern | Class | Description |
|---|---|---|
| Singleton | `GameManager` | Single instance via private constructor and static `GetInstance()` |
| Factory | `MonsterFactory` | Static creation methods for all 9 Sarawak monsters |
| Strategy | `Trainer.ChooseAction()` | Abstract method overridden by `PlayerTrainer` and `AITrainer` |

### Implemented as Planned — with Extensions

| Pattern | Extension |
|---|---|
| Factory | `MonsterFactory.Tame(WildMonster) : TamedMonster` added — converts a wild monster to a tamed one while preserving all attributes. Factory now handles both creation and conversion. |
| Factory | `MapFactory` was listed in the architecture doc but given equal prominence in the final implementation; it also appears in the Design Pattern diagram recommendation as a second Factory product line. |

### New Patterns Introduced During Development

| Pattern | Classes | Description |
|---|---|---|
| Observer / Event | `BattleManager` (publisher), `GameManager` (subscriber), `BattleAnimation` subclasses (effect) | BattleManager fires six C# events; GameManager subscribes at battle start and spawns animations. Fully decouples combat logic from visual effects. |
| Template Method | `BattleAnimation` (abstract), 9 concrete subclasses | `Update()` is concrete in the base class (frame counter, completion check); `Draw()` is abstract and implemented per subclass. Subclasses contribute only the visual behavior; lifecycle management is inherited. |

---

## 6. Increased Complexity

### 6.1 Battle Log — 14 Semantic Entry Types
The original design used a plain string list. The final implementation has 14 `LogEntryType` values covering every meaningful battle event: player moves, enemy moves, status damage ticks, status applications (by type), turn skips, separators, capture outcomes, and healing. Each type maps to a distinct display color, enabling players to scan the log at a glance.

### 6.2 Burn — Dual Effect
Originally described as a per-turn HP tick only. In the final implementation, Burned also sets `Monster.AttackMultiplier` to `0.75f`, reducing outgoing damage by 25%. This makes Burn the most strategically powerful status condition: it both damages and weakens the afflicted monster each turn.

### 6.3 Frozen — Guaranteed One-Turn Skip with Auto-Clear
The original spec described Frozen as applying to the target. The final implementation adds precise behavior: Frozen unconditionally skips exactly one turn, then the status clears automatically. This avoids the issue of a monster being frozen indefinitely, which would be unplayable.

### 6.4 Paralysis — 50% Resistance on Application
`StatusMove.Execute()` checks a 50% random resistance before applying `Paralysed`. If the target resists, the move still fires but no status is inflicted and a resistance log entry is written. This prevents Paralysis from being trivially reliable at all times.

### 6.5 Boss Monster — Three Distinct Phases
`BossMonster.UpdatePhase()` computes the current phase from HP percentage thresholds (Phase 1: HP > 66%, Phase 2: 33–66%, Phase 3: < 33%). Each phase produces a different battle cry via the overridden `GetBattleCry()`. In the final implementation, the boss's move pool and phase state are updated every turn, escalating the encounter as the player deals damage.

### 6.6 AI Type Scoring — Dual-Type Awareness
`AITrainer.ChooseAction()` was specified to evaluate type multipliers per move. The final implementation additionally handles dual-type opponents: if `state.PlayerMonster.HasDualType` is true, the AI multiplies the two type multipliers together before comparing moves. This makes the AI correctly prefer moves that are doubly effective against multi-type monsters.

### 6.7 Monster Conversion via Factory
`MonsterFactory.Tame()` converts a `WildMonster` to a `TamedMonster` by copying name, HP, level, types, battle cry, description, and moveset. The conversion preserves the wild monster's current (potentially reduced) HP level from battle, making weakening before catching a meaningful strategy rather than just a probability mechanic.

### 6.8 Encounter Level Variance
`Region.GetRandomEncounter()` adds ±2 level randomness to each encounter via factory delegation. The original design would have returned a fixed-level instance from a static pool. Level variance means the player faces different difficulty within a single region on repeated encounters, extending replayability.

---

## 7. Report-Worthy Highlights

### Section 5 — Additional Features Beyond Baseline

- **Economy system:** Gold-based resource loop between combat wins and shop item purchases, implemented entirely within `PlayerTrainer` and `GameManager` with no global state.
- **Event-driven animation system:** A publish-subscribe architecture using C# events decouples BattleManager from GameManager's rendering layer, producing nine distinct combat animations without any direct dependency between the combat engine and the display system.
- **Typed battle log:** 14 semantic `LogEntryType` values enable per-category color coding in the battle log renderer, communicating combat state at a glance without requiring the player to read every line.
- **Multi-phase boss fight:** `BossMonster.UpdatePhase()` dynamically escalates difficulty based on HP thresholds, making the final encounter structurally distinct from all trainer battles.
- **Dual-type type effectiveness:** Both combat (TypeChart multiplied for each defending type) and AI decision-making (AITrainer multiplies multipliers for dual-type opponents) correctly model the increased risk of facing dual-type monsters.
- **Flee cooldown:** `OverworldMap.ApplyFleeingCooldown()` enforces a 300-frame encounter suppression after fleeing, preventing the game from re-triggering the same encounter immediately.
- **Monster conversion:** `MonsterFactory.Tame()` performs a full attribute-preserving WildMonster → TamedMonster conversion, making pre-battle weakening a mechanically meaningful strategy.

### Section 8 — Architectural Justifications

- **Observer / Event pattern justified by SoC:** BattleManager cannot reference GameManager (doing so would couple the domain layer to the application shell). C# events invert the dependency: GameManager subscribes to BattleManager, not the reverse. This preserves testability and clean layer separation.
- **Template Method justified by animation lifecycle:** All nine animation subclasses share identical frame-counter and completion-check logic. Placing this in the abstract base class's concrete `Update()` eliminates nine copies of the same code and ensures no subclass can accidentally break the lifecycle contract.
- **Factory.Tame() justified by encapsulation:** WildMonster's constructor parameters (including internal moveset references) should not be visible to GameManager. `MonsterFactory.Tame()` encapsulates the entire conversion, keeping GameManager's capture path to a single method call.
- **LogEntry struct justified by the open/closed principle:** Adding a new log category requires only a new `LogEntryType` enum value and a new `case` in the renderer — BattleManager's log-writing code is unchanged. The typed struct made this extension point explicit.
- **`IsBattleOver(bool isEndOfRound)` justified by turn sequencing:** The parameter encodes the turn phase inside the method signature, making XP timing explicit and preventing double-award bugs that would arise if the method were called identically from both half-turns.
