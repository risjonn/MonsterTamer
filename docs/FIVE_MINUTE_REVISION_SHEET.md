# Five-Minute Revision Sheet — Monster Tamer: Guardians of Sarawak

> Read this immediately before the viva. For full detail see
> [PROJECT_KNOWLEDGE_GUIDE.md](PROJECT_KNOWLEDGE_GUIDE.md) and
> [INTERVIEW_QA_AND_CODE_REFERENCE.md](INTERVIEW_QA_AND_CODE_REFERENCE.md).

---

## 1. Project Summary

**What it is:** A 2D tile-based monster-collecting RPG (C# + SplashKit) called *Monster Tamer:
Guardians of Sarawak*. Run with `skm dotnet run` (never plain `dotnet run`).

**Game loop:** Explore (overworld) → random/trainer encounter → turn-based battle → win/lose/capture
→ progress through 3 regions (Rainforest, River Delta, Ancient Highlands) → multi-phase final boss
(Naga Belian).

**Main systems:**
- **Domain model** — `Monster`, `Move`, `Item`, `Trainer` (4 abstract base classes) + concrete
  subclasses, `TypeChart`, `Enumerations`. No SplashKit code here.
- **Containers** — `Party` (max 6), `Inventory`, `Region`, `BattleState`, `LogEntry`.
- **Orchestration** — `BattleManager` (runs one battle), `MonsterFactory` (builds monsters).
- **Presentation** — `GameManager` (Singleton, game loop, state machine), `OverworldMap`,
  `BattleAnimation`, `Program`.

---

## 2. Architecture Summary

### GameManager
- **Singleton** (`GetInstance()`, private constructor). Owns the SplashKit `Window`, the player
  (`PlayerTrainer`), the 3 `Region`s, the `OverworldMap`, and `_battleManager`.
- It is a **state machine** driven by `GameState` enum: `MainMenu, Overworld, EncounterPrompt,
  Battle, PartyScreen, CaptureScreen, Victory, GameOver`.
- Each frame: `ProcessEvents()` → `HandleInput()` → `Update()` → `Draw()` (switch on `_state`).
- Never calculates damage/type/capture math — always delegates.

### BattleManager
- Created **fresh per battle** (not a singleton, no state across battles).
- Fields: `_player` (PlayerTrainer), `_opponent` (**Trainer base type**), `_wildTarget`,
  `_bossTarget`, `_battleLog`, `_turnNumber`.
- `EnemyMonster => (Monster?)_wildTarget ?? (Monster?)_bossTarget ?? _opponent.GetActiveMonster();`
  — single property used everywhere instead of "is this a wild battle?" checks.
- `PlayerTurn(moveIndex)` / `EnemyTurn()` — apply status damage → check
  Frozen/Paralysed skip → `move.Execute(attacker, defender)` polymorphically → log result.
- Fires **Observer events**: `AttackHit`, `HealUsed`, `StatusApplied`, `StatusDamage`,
  `TurnSkipped`, `StatusRemoved`.

### Monster hierarchy
```
Monster (abstract)            -- _hp, _maxHp, _level, types, _statusCondition
 ├─ TamedMonster               -- + _experience, GainExperience()/LevelUp()
 ├─ WildMonster                -- + _catchRate (encounter-table monsters)
 └─ BossMonster                -- + _phase/_totalPhases, UpdatePhase()
```
Abstract methods: `GetBattleCry()`, `GetDescription()`. Each composes a `Move[] _moveSet`.
`MonsterFactory.Tame(WildMonster)` → builds a new `TamedMonster` (Experience always resets to 0).

### Trainer hierarchy
```
Trainer (abstract)             -- _name, _party (Party), _bag (Inventory)
 ├─ PlayerTrainer               -- _gold, _selectedMoveIndex, _selectedPartyIndex
 └─ AITrainer                   -- _difficulty (stored, unused), _selectedMoveIndex
```
`ChooseAction(BattleState) : TrainerAction` — **abstract** (Strategy Pattern).
- `PlayerTrainer.ChooseAction()` → always `UseMove` (decision already made via keyboard).
- `AITrainer.ChooseAction()` → loops `MoveSet`, picks move with strictly-highest type multiplier
  vs player's monster type(s).

---

## 3. OOP Principles Cheat Sheet

| Principle | Definition (1 line) | Project Example |
|---|---|---|
| **Abstraction** | Expose *what* an object does, hide *how*, via abstract methods. | `Move.Execute(Monster, Monster)` — every move "does something" but `AttackMove`/`HealMove`/`StatusMove` implement it completely differently. |
| **Encapsulation** | All state is `private`; access only via properties/methods that enforce rules. | `Monster.TakeDamage(int)` clamps HP to `[0, MaxHp]` — `_hp` can never be set directly or go negative. |
| **Inheritance** | Concrete classes extend a base with `: ParentClass`, reusing shared state/behaviour. | `TamedMonster : Monster`, `AttackMove : Move`, `AITrainer : Trainer`, `CaptureOrb : Item`. |
| **Polymorphism** | Calling a method on a base-type reference runs the subclass's override. | `BattleManager` calls `selectedMove.Execute(attacker, defender)` where `selectedMove` is typed `Move` — resolves to `AttackMove`/`HealMove`/`StatusMove` at runtime. |
| **Composition** | Owner creates and is solely responsible for the lifetime of the contained object. | `Trainer` constructs its own `Party` and `Inventory` in its constructor — they don't outlive it and aren't shared. |

(Bonus: **Aggregation** — `Region` and `OverworldMap`'s `TrainerNPC` both hold references to the
*same* `AITrainer` instance, neither owns its lifetime.)

---

## 4. Design Pattern Cheat Sheet

### Singleton
- **Class:** `GameManager`
- **Why:** There is conceptually exactly one game/window/loop. Private constructor +
  `GetInstance()` prevents a second `Window` from ever being created. Lets `Program.Main` be a
  single line: `GameManager.GetInstance().Run();`

### Factory
- **Class:** `MonsterFactory` (also `MapFactory`)
- **Why:** Building a monster needs ~7 args incl. a hand-built `Move[]`. One static method per
  species (`CreateKelabit`, `CreateBekantan`, ...) centralises that recipe so call sites
  (`SetupRegions`, `Region.GetRandomEncounter`, capture logic) don't repeat it. `Tame(WildMonster)`
  is the single wild→tamed conversion point. `MapFactory` does the same for the 3 region tile grids.

### Strategy
- **Class:** `Trainer.ChooseAction(BattleState)` — abstract, implemented by `PlayerTrainer` and
  `AITrainer`
- **Why:** "What move does the opponent pick?" differs between human input and AI computation,
  but `BattleManager.EnemyTurn()` should not need `if (opponent is AITrainer)`. Each `Trainer`
  subclass is a concrete strategy.
- **Caveat:** the **boss** does NOT use this pattern — its move selection is hard-coded inline in
  `BattleManager.EnemyTurn()` (a dummy empty `AITrainer("Naga Belian")` exists only to satisfy the
  constructor signature).

### Observer
- **Class:** `BattleManager` events (`AttackHit`, `HealUsed`, `StatusApplied`, `StatusDamage`,
  `TurnSkipped`, `StatusRemoved`) — `GameManager` subscribes
- **Why:** `BattleManager` must not know about pixels/sprites (per project rules), but
  `GameManager` needs to react the instant an attack lands to spawn a `BattleAnimation`. Decouples
  battle *logic* from battle *visuals* — `BattleManager` could run headless with zero subscribers.
- **Caveat:** `StatusDamage` and `StatusRemoved` are fired but have **zero subscribers** currently
  (logged as text only, no animation).

---

## 5. Important Algorithms

### Damage calculation (`AttackMove.Execute`)
```
multiplier = TypeChart.GetMultiplier(move.MoveType, target.PrimaryType)
if target.HasDualType: multiplier *= TypeChart.GetMultiplier(move.MoveType, target.SecondaryType)

baseDamage  = (move.Power + caster.Level * 2) * caster.AttackMultiplier   // 0.75 if Burned
finalDamage = (int)(baseDamage * multiplier)
target.TakeDamage(finalDamage)
```
- Dual-type multipliers **multiply together** → range is `0.0x` to `4.0x` (not capped at 2x).
- "Super effective!" text only fires for a combined multiplier of **exactly** `2.0f`/`0.5f`/`0.0f`
  — a `4.0f` result prints no phrase even though damage is quadrupled.
- `AttackMultiplier` (0.75 if Burned) reduces the **attacker's own** output, not damage received.

### Type effectiveness (`TypeChart`)
- Static `8x8 float[,] _chart`, indexed `[(int)attackType, (int)defendType]` → `O(1)` lookup.
- 8 types in order: `Normal, Fire, Water, Grass, Electric, Flying, Fighting, Steel`.
- One table drives 3 systems: damage calc, AI move selection, and UI "super effective" hints.

### Capture formula (`CaptureOrb.AttemptCapture`)
```
catchChance = (1 - target.HpPercent) * target.CatchRate * _captureModifier
roll = Random.NextDouble()

roll <= catchChance         -> Caught
roll <= catchChance * 1.5   -> Escaped
else                         -> Failed
```
- At full HP, `catchChance = 0` → Caught is impossible, Escaped also impossible → always `Failed`.
- `CatchRate` fixed per species (0.30 Buaya → 0.70 Pelanduk); `_captureModifier` fixed per orb
  (1.0 standard).

### AI move selection
**Regular `AITrainer` and Boss** (same algorithm, implemented twice — boss is hard-coded inline):
```
bestMultiplier = -1; bestIndex = 0
for each move in MoveSet:
    mult = TypeChart.GetMultiplier(move.MoveType, defenderPrimaryType)
    if defender.HasDualType: mult *= GetMultiplier(move.MoveType, defenderSecondaryType)
    if mult > bestMultiplier:   // strict > => ties go to earliest move
        bestMultiplier = mult; bestIndex = i
```
Purely defensive type-matching — ignores move Power, attacker's own type (no STAB), and HP.

**Wild monsters:** fully random — `new Random().Next(moveset.Length)`. No type reasoning.

---

## 6. Common Viva Questions (Quick Answers)

1. **Why is `BattleManager` separate from `GameManager`?**
   Separation of concerns — `BattleManager` has zero SplashKit dependencies and could run headless;
   `GameManager` never computes damage/type/capture math, only calls into `BattleManager` and
   `Move`/`TypeChart`/`CaptureOrb`.

2. **Why is `GameManager` a Singleton?**
   Conceptually one game/window/loop exists. Private constructor + `GetInstance()` stop a second
   `Window` ever being created; any code can reach "the game" without passing references around.

3. **How does polymorphism show up in move execution?**
   `BattleManager` calls `selectedMove.Execute(attacker, defender)` on a `Move`-typed reference;
   the actual call resolves to `AttackMove`, `HealMove`, or `StatusMove` depending on runtime type
   — no `if/else` on move category.

4. **How is encapsulation enforced?**
   Every field in every domain class is `private`. Access only via expression-bodied properties
   (`public string Name => _name;`) or methods that enforce invariants, e.g. `TakeDamage(int)`
   clamps HP to `[0, MaxHp]`.

5. **Explain the Strategy pattern in this project — and its exception.**
   `Trainer.ChooseAction(BattleState)` is abstract; `PlayerTrainer` returns `UseMove` (decision
   already made via input), `AITrainer` picks the best type-matchup move. The **boss** is the
   exception — its AI is hard-coded inline in `BattleManager.EnemyTurn()`, not via `ChooseAction`.

6. **How does the Factory pattern simplify monster creation?**
   `MonsterFactory` has one static method per species encapsulating ~7 constructor args + a
   hand-built `Move[]`. Adding a new monster = one new static method; no other code changes.
   `Tame(WildMonster)` is the single wild→tamed bridge.

7. **Walk me through the damage formula and an edge case.**
   `(power + level*2) * attackMultiplier * typeMultiplier`. Edge case: dual-type targets multiply
   both type multipliers together, so a `2.0 * 2.0 = 4.0x` hit is possible but prints **no**
   "super effective" text (that only triggers on exactly 2.0/0.5/0.0).

8. **What happens with status conditions — can a monster have two at once?**
   No — `StatusCondition` is a plain (non-flags) enum, one value at a time. `StatusMove.Execute()`
   fails outright if the target already has a status. Frozen/Paralysed skip exactly one turn then
   auto-clear; Burn/Poison deal 10%/8% max-HP damage per turn with no auto-clear (only
   `StatusCure` removes them).

9. **How does the Observer pattern decouple battle logic from visuals?**
   `BattleManager` exposes events (`AttackHit`, `HealUsed`, `StatusApplied`, `TurnSkipped`, etc.).
   `GameManager` subscribes in `StartWildBattle`/`StartTrainerBattle`/`StartBossBattle` and spawns
   `BattleAnimation` objects in response. `BattleManager` never knows animations exist.

10. **What's the capture formula and why can't you catch a full-HP monster?**
    `catchChance = (1 - HpPercent) * CatchRate * captureModifier`. At full HP, `HpPercent = 1` so
    `catchChance = 0`; since `Escaped` requires `roll <= catchChance * 1.5` (also 0), the only
    possible outcome is `Failed` — the player must lower HP first.
