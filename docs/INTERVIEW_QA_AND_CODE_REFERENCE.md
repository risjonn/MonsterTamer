# Interview Q&A and Code Reference — Viva Preparation

This document is a rapid-recall companion for your project presentation and viva. Each entry has:

- **Question** — what the examiner is likely to ask
- **Expected Answer** — an examiner-level answer grounded in this codebase
- **Show Me The Code** — exactly where to navigate if asked "show me where you implemented this"

Cross-reference: [PROJECT_KNOWLEDGE_GUIDE.md](PROJECT_KNOWLEDGE_GUIDE.md) has the deeper narrative
walkthrough this document distills into Q&A form.

---

## 1. Project Overview

### Q1. What is this project, and what is the core gameplay loop?

**Expected Answer:**
"Monster Tamer: Guardians of Sarawak" is a 2D, tile-based monster-collecting RPG built in C# with
SplashKit. The player explores three Sarawak-themed regions (Rainforest, River Delta, Ancient
Highlands) on foot, triggers random wild-monster encounters in tall grass, battles AI trainers,
captures wild monsters with Capture Orbs, builds a 6-monster party, and finally fights a
multi-phase boss (Naga Belian) after clearing all three regions. The loop is:
**explore → encounter → turn-based battle → capture/win/lose → progress region → repeat → boss**.

**Show Me The Code:**
- File: `src/Program.cs` — entry point, calls `GameManager.GetInstance().Run()`
- File: `src/GameManager.cs`, method `Run()` (line 182) — the main game loop
- File: `src/GameManager.cs`, method `SetupGame()` (line 112) — builds the starter, bag, regions, map

---

### Q2. What technology stack does the project use, and how do you build/run it?

**Expected Answer:**
The project is a .NET 8 C# console/graphics application using **SplashKit** for windowing,
input, drawing, audio, and timers. It must always be run with `skm dotnet run` (not plain
`dotnet run`) because SplashKit's native libraries need to be loaded by the `skm` wrapper —
plain `dotnet run` compiles fine but crashes at runtime on the first SplashKit call.

**Show Me The Code:**
- File: `CLAUDE.md` — Build & Run Commands section
- File: `src/GameManager.cs` constructor (line 88) — first SplashKit call (`new Window(...)`)

---

### Q3. Walk me through what happens between launching the program and seeing the main menu.

**Expected Answer:**
1. `Program.Main()` calls `GameManager.GetInstance()`.
2. Since `_instance` is `null`, the private constructor runs: creates a 1280×720 `Window`,
   toggles fullscreen, sets `_state = GameState.MainMenu`, creates the `PlayerTrainer("Ranger")`,
   and calls `SetupGame()`.
3. `SetupGame()` creates the starter monster (`MonsterFactory.CreateKelabit(level: 8)`), adds
   starting items (2 Potions, 3 Capture Orbs) to the bag, calls `SetupRegions()` to build all 3
   regions with their encounter tables and AI trainers, then `LoadOverworldMap(0)` to build
   region 0's tile grid and trainer NPCs.
4. `GetInstance()` returns the singleton, and `Run()` is called: starts the `"blink"` SplashKit
   timer, loads all sprites and BGM, then enters the `while (!_window.CloseRequested)` loop,
   calling `ProcessEvents()`, `HandleInput()`, `Update()`, `Draw()` every frame.
5. `Draw()` dispatches on `_state`, which is `MainMenu`, so `DrawMainMenu()` runs.

**Show Me The Code:**
- File: `src/Program.cs`
- File: `src/GameManager.cs`, constructor (line 88), `GetInstance()` (line 102), `SetupGame()` (line 112), `Run()` (line 182)

---

## 2. Architecture

### Q4. Describe the overall architecture / layers of the codebase.

**Expected Answer:**
The codebase is organized into four conceptual layers:
1. **Domain model** (`Monster`, `Move`, `Item`, `Trainer` and their concrete subclasses,
   `TypeChart`, `Enumerations`) — pure game rules, zero SplashKit dependencies.
2. **Containers/data** (`Party`, `Inventory`, `Region`, `BattleState`, `LogEntry`) — hold
   collections of domain objects.
3. **Game logic/orchestration** (`BattleManager`, `MonsterFactory`) — runs a single battle and
   builds fully-configured monsters.
4. **Presentation/engine glue** (`GameManager`, `OverworldMap`, `BattleAnimation`, `Program`) —
   the SplashKit window, game loop, input handling, and all drawing.

This enforces the project rule that **battle logic must not be in `GameManager`** and **drawing
logic must not be in game-logic classes** — clean separation of concerns.

**Show Me The Code:**
- Domain: `src/Monster.cs`, `src/Move.cs`, `src/Item.cs`, `src/Trainer.cs`, `src/TypeChart.cs`
- Containers: `src/Party.cs`, `src/Inventory.cs`, `src/Region.cs`, `src/BattleState.cs`, `src/LogEntry.cs`
- Orchestration: `src/BattleManager.cs`, `src/MonsterFactory.cs`
- Presentation: `src/GameManager.cs`, `src/OverworldMap.cs`, `src/BattleAnimation.cs`

---

### Q5. Why is `BattleManager` a separate class from `GameManager`? Why not just put battle code in `GameManager`?

**Expected Answer:**
**Separation of concerns.** `BattleManager` knows nothing about pixels, windows, sprites, or
SplashKit — it operates purely on `Monster`, `Move`, `Trainer` domain objects and produces
result strings and events. `GameManager` knows nothing about how damage, type multipliers, or
status effects are calculated — it only calls `_battleManager.PlayerTurn(index)` /
`.EnemyTurn()` and reacts to the results/events to draw animations. This means `BattleManager`
could run "headless" (e.g., in a unit test) with zero SplashKit and zero subscribers, and behave
identically. A new `BattleManager` instance is created fresh for every battle — it holds no
state across battles.

**Show Me The Code:**
- File: `src/BattleManager.cs`, class `BattleManager` (line 6) — no `using SplashKit`, no `Draw()`
- File: `src/GameManager.cs`, methods `StartWildBattle` (line 643), `HandleBattleInput` (line 304) — `GameManager` only calls into `BattleManager`, never computes damage itself

---

### Q6. Explain the `GameState` state machine. How do screens/transitions work?

**Expected Answer:**
`GameManager` is essentially a finite state machine driven by the `GameState` enum:
`MainMenu, Overworld, EncounterPrompt, Battle, PartyScreen, CaptureScreen, Victory, GameOver`.
Every frame, `HandleInput()`, `Update()`, and `Draw()` branch on `_state` (a private field).
Transitions are simple assignments — e.g., pressing Enter on the main menu sets
`_state = GameState.Overworld`; triggering a wild encounter sets
`_state = GameState.EncounterPrompt`; confirming the encounter calls `StartWildBattle()` which
sets `_state = GameState.Battle`. `Draw()` is a single `switch` on `_state` that dispatches to
one `DrawXxx()` method per screen.

**Show Me The Code:**
- File: `src/Enumerations.cs`, enum `GameState` (line 15)
- File: `src/GameManager.cs`, method `Draw()` (line 624) — the screen-dispatch switch
- File: `src/GameManager.cs`, method `HandleInput()` (line 198) — input dispatch switch

---

### Q7. How does the overworld map work — tiles, movement, and encounter triggers?

**Expected Answer:**
`OverworldMap` owns a `Tile[25,20]` grid (each `Tile` wraps a `TileType` enum and computes
`Walkable` — false for `Tree`, `Water`, `House`), an `OverworldPlayer` (pixel position, facing
direction, 2-frame walk animation), a list of `TrainerNPC`s, and a camera offset.
`Update(up, down, left, right)`:
1. `TryMove(dx, dy)` — only moves the player if the destination tile is walkable.
2. `CheckTriggers()` — inspects the tile the player now stands on:
   - `TallGrass` (new tile + cooldown clear) → 15% roll → `WildEncounterTriggered = true`.
   - `RegionExit` → starts a fade-to-black, then sets `RegionExitTriggered = true`.
   - `Shop` → `ShopEncounterTriggered = true`.
   - Trainer within 1.5 tiles, not yet defeated, cooldown clear → `TrainerEncounterTriggered = true`.

`GameManager.Update()` reads these one-frame flags and converts them into `GameState`
transitions. `MapFactory` (Factory pattern) builds the three regions' tile grids from
hand-coded `int[20,25]` arrays using readable single-letter constants (`T`=Tree, `g`=TallGrass,
`X`=RegionExit, etc.).

**Show Me The Code:**
- File: `src/OverworldMap.cs`, class `Tile` (line 22), class `OverworldMap` (line 101)
- File: `src/OverworldMap.cs`, method `Update()` (line 194), `TryMove()` (line 239), `CheckTriggers()` (line 254)
- File: `src/OverworldMap.cs`, class `MapFactory` (line 566), e.g. `CreateRainforestMap()` (line 579)
- File: `src/GameManager.cs`, method `Update()` (line 522) — reads the trigger flags

---

## 3. OOP Principles

### Q8. Explain Abstraction in this project, using `Monster` as an example.

**Expected Answer:**
`Monster` is an `abstract class` that defines the shared "creature" contract — fields for HP,
level, type(s), status condition — plus concrete shared operations (`TakeDamage`, `RestoreHp`,
`LevelUp`, `ApplyStatusEffect`) that work identically for every monster. But it declares
`GetBattleCry()` and `GetDescription()` as `abstract` — every subclass *must* provide its own
flavour text, but `Monster` itself says nothing about *what* that text is. This lets
`BattleManager`, `TypeChart`, and every `Move` subclass operate on a plain `Monster` reference
without caring whether it's a `TamedMonster`, `WildMonster`, or `BossMonster`. The same pattern
applies to `Move.Execute()`, `Item.Use()`, and `Trainer.ChooseAction()` — each says "this kind of
thing can do X" without saying how.

**Show Me The Code:**
- File: `src/Monster.cs`, class `Monster` (line 4), abstract methods `GetBattleCry()` / `GetDescription()` (lines 100–101)
- File: `src/Move.cs`, abstract methods `Execute()` / `GetMoveCategory()` (lines 28–29)
- File: `src/Item.cs`, abstract methods `Use()` / `GetDescription()` (lines 23–24)
- File: `src/Trainer.cs`, abstract method `ChooseAction()` (line 33)

---

### Q9. Why must abstract class constructors be `protected`, not `public`?

**Expected Answer:**
A `public` constructor would imply you can do `new Monster(...)` directly — but `Monster` is
incomplete on its own (it has no `GetBattleCry()`/`GetDescription()` implementation, so C#
wouldn't even allow instantiation, but conceptually it should never be constructed standalone).
`protected` means **only subclasses** can call it via `: base(...)`, which is exactly how
`Monster` is used — `TamedMonster`, `WildMonster`, and `BossMonster` each call one of `Monster`'s
two `protected` constructors (single-type or dual-type) to initialize the shared fields, then add
their own. This is enforced project-wide for `Monster`, `Move`, `Item`, and `Trainer`.

**Show Me The Code:**
- File: `src/Monster.cs`, lines 17 and 29 — both constructors marked `protected Monster(...)`
- File: `src/ConcreteMonsters.cs`, line 16/28 (`TamedMonster`), line 68/80 (`WildMonster`), line 108 (`BossMonster`) — all use `: base(...)`

---

### Q10. Explain Encapsulation. How are fields protected from outside misuse?

**Expected Answer:**
Every field in every domain class is `private` (e.g., `_name`, `_hp`, `_maxHp` in `Monster`).
External code can only read state through expression-bodied properties
(`public int Hp => _hp;`) and can only mutate state through methods that enforce invariants.
For example, `TakeDamage(int amount)` does `_hp -= amount; if (_hp < 0) _hp = 0;` — it is
*impossible* for any external caller to push `Hp` below zero or above `MaxHp`, because `_hp`
itself is never exposed for direct assignment. Where a setter genuinely needs to be public (e.g.
`Monster.Status`), it's a property with explicit `get`/`set`, not a public field — and
`PlayerTrainer.SelectedMoveIndex` goes further, clamping the value in its `set` block.

**Show Me The Code:**
- File: `src/Monster.cs`, fields (lines 7–14, all `private`), `TakeDamage()` (line 60), `RestoreHp()` (line 66)
- File: `src/ConcreteTrainers.cs`, property `SelectedMoveIndex` (line 32) — clamps to `active.MoveSet.Length`

---

### Q11. Walk me through the `SelectedMoveIndex` property in `PlayerTrainer` — why is it written this way?

**Expected Answer:**
`SelectedMoveIndex` is a property with a custom `set` accessor. Whenever `GameManager` changes
the selected move (arrow-key navigation), the setter immediately clamps the value to
`[0, active.MoveSet.Length - 1]` based on the player's *currently active* monster. This matters
because different monsters can have different numbers of moves (or fewer, if not all 4 slots are
filled) — without this clamp, switching to a monster with 2 moves while move index 3 was
selected would cause an `IndexOutOfRangeException` in `BattleManager.PlayerTurn`. This is
encapsulation enforcing a runtime invariant at the point of mutation, not after the fact.

**Show Me The Code:**
- File: `src/ConcreteTrainers.cs`, property `SelectedMoveIndex` (lines 32–47)

---

### Q12. How does `Monster.TakeDamage()` prevent invalid HP states, and how is it used?

**Expected Answer:**
```csharp
public void TakeDamage(int amount)
{
    _hp -= amount;
    if (_hp < 0) _hp = 0;
}
```
It clamps HP to a minimum of 0 — `Hp` can never be negative, which would otherwise break
`IsAlive => _hp > 0` and `HpPercent => (float)_hp / _maxHp` (which could go negative and confuse
HP-bar rendering). Symmetrically, `RestoreHp(int amount)` clamps to `MaxHp`. Both are called only
through higher-level operations: `AttackMove.Execute()` calls `target.TakeDamage(finalDamage)`,
and `Monster.ApplyStatusEffect()` calls `TakeDamage()` internally for burn/poison ticks.

**Show Me The Code:**
- File: `src/Monster.cs`, `TakeDamage()` (line 60), `RestoreHp()` (line 66), `IsAlive`/`HpPercent` (lines 50–51)
- File: `src/ConcreteMoves.cs`, `AttackMove.Execute()` (line 21) — `target.TakeDamage(finalDamage)`

---

### Q13. Explain Inheritance using the `Monster` hierarchy, including constructor chaining.

**Expected Answer:**
`TamedMonster : Monster`, `WildMonster : Monster`, `BossMonster : Monster` — all three explicitly
extend `Monster`. `Monster` itself demonstrates **constructor overloading**: it has two
`protected` constructors — one for single-type monsters (`Monster(name, maxHp, level,
primaryType)`) and one for dual-type monsters (adds `secondaryType`, sets `_hasDualType = true`).
Each concrete subclass mirrors this with its own two constructor overloads, each chaining to the
matching base constructor via `: base(...)`. For example, `TamedMonster` has a 8-arg
dual-type constructor calling `base(name, maxHp, level, primary, secondary)` and a 7-arg
single-type constructor calling `base(name, maxHp, level, primary)`. This avoids duplicating the
HP/level/type initialization logic in three places.

**Show Me The Code:**
- File: `src/Monster.cs`, constructors (lines 17 and 29)
- File: `src/ConcreteMonsters.cs`, `TamedMonster` constructors (lines 12 and 24), `WildMonster` constructors (lines 64 and 76), `BossMonster` constructor (line 105, dual-type only)

---

### Q14. Explain Inheritance using the `Move` hierarchy — `AttackMove`, `HealMove`, `StatusMove`.

**Expected Answer:**
`Move` is abstract, holding `_name`, `_power`, `_manaCost`, `_moveType` and declaring
`Execute(Monster caster, Monster target) : string` and `GetMoveCategory() : string` as abstract.
- `AttackMove : Move` — fixed `manaCost = 10`; `Execute()` looks up type multipliers via
  `TypeChart`, computes damage, calls `target.TakeDamage()`.
- `HealMove : Move` — fixed `manaCost = 15`; `Execute()` calls `caster.RestoreHp()` — note it
  heals the **caster**, not the target.
- `StatusMove : Move` — fixed `manaCost = 12`, `Power = 0`; `Execute()` sets `target.Status` to
  the move's `_inflicts` value, with special-case logic for paralysis (50% fail chance) and
  failing outright if the target already has a status.

Each subclass calls `base(name, power, manaCost, moveType)` with its own hard-coded `manaCost`
— this is inheritance encoding "all attack moves cost 10 mana" as a class-level invariant rather
than a per-instance value.

**Show Me The Code:**
- File: `src/Move.cs`, class `Move` (line 5), abstract `Execute`/`GetMoveCategory` (lines 28–29)
- File: `src/ConcreteMoves.cs`, `AttackMove` (line 5), `HealMove` (line 39), `StatusMove` (line 57)

---

### Q15. Explain Inheritance using the `Trainer` hierarchy — `PlayerTrainer` vs `AITrainer`.

**Expected Answer:**
`Trainer` is abstract, holding `_name`, `_party` (a `Party`), and `_bag` (an `Inventory`) — both
created inside the `Trainer` constructor (composition). It provides a concrete shared method,
`GetActiveMonster()`, which returns the first alive `TamedMonster` in `_party`, and declares
`ChooseAction(BattleState state) : TrainerAction` as abstract.
- `PlayerTrainer : Trainer` adds `_gold`, `_selectedMoveIndex`, `_selectedPartyIndex`. Its
  `ChooseAction()` always returns `TrainerAction.UseMove` — the real "decision" already happened
  via keyboard input setting `SelectedMoveIndex`.
- `AITrainer : Trainer` adds `_difficulty` and `_selectedMoveIndex`. Its `ChooseAction()` actually
  computes a decision — it loops the active monster's moves and picks the one with the best type
  multiplier against the player's monster.

**Show Me The Code:**
- File: `src/Trainer.cs`, class `Trainer` (line 6), `GetActiveMonster()` (line 25), abstract `ChooseAction()` (line 33)
- File: `src/ConcreteTrainers.cs`, `PlayerTrainer` (line 5), `AITrainer` (line 59)

---

### Q16. Explain Polymorphism using `Move.Execute()` being called from `BattleManager`.

**Expected Answer:**
In `BattleManager.PlayerTurn()`, the line is:
```csharp
Move selectedMove = attacker.MoveSet[moveIndex];
string result = selectedMove.Execute(attacker, defender);
```
`selectedMove` is *statically typed* as `Move` — `BattleManager` has no `if (selectedMove is
AttackMove)` branching. At runtime, the actual object could be an `AttackMove`, `HealMove`, or
`StatusMove`, and the CLR dispatches to the correct override automatically. `AttackMove.Execute`
damages the target; `HealMove.Execute` heals the caster; `StatusMove.Execute` inflicts a status.
This is the textbook "one interface, many implementations" — and it's why adding a 4th move type
in future would require zero changes to `BattleManager`.

**Show Me The Code:**
- File: `src/BattleManager.cs`, `PlayerTurn()` line 110 (`selectedMove.Execute(attacker, defender)`) and `EnemyTurn()` line 204
- File: `src/ConcreteMoves.cs` — three different `Execute()` overrides (lines 11, 45, 68)

---

### Q17. Explain Polymorphism using `Trainer.ChooseAction()`.

**Expected Answer:**
`BattleManager.EnemyTurn()` calls `_opponent.ChooseAction(state)` where `_opponent` is declared
as the **base type** `Trainer`. If `_opponent` is an `AITrainer`, this runs the type-matchup
loop and sets `_selectedMoveIndex`; if it were (hypothetically) a second `PlayerTrainer`, it
would just return `TrainerAction.UseMove` without picking a move. `BattleManager` never asks
"what kind of trainer is this" before calling `ChooseAction` — the correct behaviour is selected
purely by the runtime type of the object referenced by `_opponent`. This is also the **Strategy
pattern** in action (see Q24).

**Show Me The Code:**
- File: `src/BattleManager.cs`, field `private Trainer _opponent;` (line 9), call site in `EnemyTurn()` (line 158)
- File: `src/ConcreteTrainers.cs`, `PlayerTrainer.ChooseAction()` (line 51), `AITrainer.ChooseAction()` (line 74)

---

### Q18. Explain Polymorphism using `BattleAnimation`.

**Expected Answer:**
`BattleAnimation` is abstract with `_frame`/`_duration`, a concrete `Update()` that increments
`_frame`, a computed `IsComplete => _frame >= _duration`, and an abstract `Draw()`. There are 8
concrete subclasses (`HitFlashAnimation`, `DamageNumberAnimation`, `HealFlashAnimation`,
`HealNumberAnimation`, `HealParticleAnimation`, `StatusSwirlAnimation`, `CaptureOrbAnimation`,
`PotionGlowAnimation`, `TurnSkipAnimation`). `GameManager` keeps a single
`List<BattleAnimation> _battleAnimations` and every frame calls `.Update()` and `.Draw()` on each
element through the base-type reference — it never checks which concrete animation it is. New
animations are added by event handlers (`OnAttackHit`, `OnHealUsed`, etc.) reacting to
`BattleManager` events.

**Show Me The Code:**
- File: `src/BattleAnimation.cs`, abstract class `BattleAnimation` (line 8) and 8 subclasses (lines 32–296)
- File: `src/GameManager.cs`, field `_battleAnimations` (line 53), update loop in `Update()` (lines 529–535)

---

### Q19. Give an example of Composition in this codebase and explain why it's composition (not aggregation).

**Expected Answer:**
`Trainer` **composes** `Party` and `Inventory` — both are constructed with `new` *inside* the
`Trainer` constructor itself. No external code can hand a `Trainer` a pre-built `Party`; the
`Party`/`Inventory` are created with the `Trainer` and have no independent existence or lifetime
— if the `Trainer` object is discarded, so is its `Party`. Similarly, every `TamedMonster` /
`WildMonster` / `BossMonster` composes its own `Move[] _moveSet` — the moves are built inline by
`MonsterFactory` specifically for that monster instance and are never shared with another
monster.

**Show Me The Code:**
- File: `src/Trainer.cs`, constructor (around line 18-22, where `_party = new Party(); _bag = new Inventory();`)
- File: `src/MonsterFactory.cs`, e.g. `CreateKelabit()` (line 23) — builds a `new Move[] { ... }` inline

---

### Q20. Give an example of Aggregation and explain why it's aggregation (not composition).

**Expected Answer:**
`Region` **aggregates** `AITrainer`s. The same `AITrainer` instances created in
`GameManager.SetupRegions()` are stored both in `Region._trainers` (via `AddTrainer`) **and**
referenced by `TrainerNPC.Trainer` on the `OverworldMap`. Two different objects (`Region` and
`OverworldMap`/`TrainerNPC`) hold references to the *same* `AITrainer` — neither owns its
lifetime exclusively, and the `AITrainer`'s `Party`/`Bag` state (e.g., which monsters have
fainted) is shared and visible through both paths. Similarly, `BattleManager` aggregates
`_player` (a `PlayerTrainer`) — it's passed in via the constructor and continues to exist
(as the same object, with all its progress) after the `BattleManager` is discarded at the end of
the battle.

**Show Me The Code:**
- File: `src/GameManager.cs`, `SetupRegions()` (line 127) — `AITrainer t1 = new AITrainer(...)`, then `rainforest.AddTrainer(t1)`
- File: `src/GameManager.cs`, `LoadOverworldMap()` (line 158) — `npcs.Add(new TrainerNPC(npcX, npcY, t, t.Name))` using the *same* `t`
- File: `src/Region.cs`, `Trainers` property (line 76); `src/OverworldMap.cs`, class `TrainerNPC` (line 82), property `Trainer` (line 86)

---

## 4. Design Patterns

### Q21. Explain the Singleton pattern as used for `GameManager`.

**Expected Answer:**
```csharp
private static GameManager? _instance = null;
private GameManager() { /* build window, player, regions */ }
public static GameManager GetInstance()
{
    if (_instance == null) _instance = new GameManager();
    return _instance;
}
```
The constructor is `private`, so the only way to obtain a `GameManager` is through
`GetInstance()`, which lazily creates exactly one instance. This is appropriate because there is
conceptually exactly **one** game running — one `Window`, one player, one game loop. A second
instance would open a second SplashKit window and run a duplicate loop. `Program.Main` is
therefore a single line: `GameManager.GetInstance().Run();`.

**Show Me The Code:**
- File: `src/GameManager.cs`, field `_instance` (line 11), constructor (line 88, `private`), `GetInstance()` (line 102)
- File: `src/Program.cs`

---

### Q22. Explain the Factory pattern as used for `MonsterFactory` (and `MapFactory`).

**Expected Answer:**
Constructing a monster requires ~7 constructor arguments including a hand-built `Move[]` array.
`MonsterFactory` provides one static method per species (`CreateKelabit`, `CreateBekantan`, ...,
`CreateNagaBelian`, each with a default `level` parameter) that encapsulates this construction
recipe — every call site (`SetupRegions`, `Region.GetRandomEncounter` via
`CreateFreshEncounter`, the capture flow) just calls `MonsterFactory.CreateBekantan(level: 8)`
without knowing or repeating the stat/move details. `Tame(WildMonster monster)` is the single
conversion point from a `WildMonster` to a `TamedMonster`, picking the correct constructor
overload based on `monster.HasDualType`. `MapFactory` applies the same idea to `OverworldMap`:
three static methods, each owning a hard-coded tile grid for one region.

**Advantage:** adding a 9th monster species means adding **one** new static method — no other
code changes its construction logic.

**Show Me The Code:**
- File: `src/MonsterFactory.cs`, class `MonsterFactory` (line 6), `Tame()` (line 8), e.g. `CreateKelabit()` (line 23), `CreateNagaBelian()` (line 174)
- File: `src/OverworldMap.cs`, class `MapFactory` (line 566), `CreateRainforestMap()` (line 579)

---

### Q23. Where exactly is the Factory pattern *used* (call sites)?

**Expected Answer:**
Three call sites:
1. `GameManager.SetupGame()` / `SetupRegions()` — builds the starter and every region's encounter
   templates and AI trainer parties via `MonsterFactory.CreateXxx(...)` and `CreateXxxTamed(...)`.
2. `Region.CreateFreshEncounter(name, level)` — switches on the encounter template's name and
   calls the matching `MonsterFactory.CreateXxx(level)` to produce a **brand-new** `WildMonster`
   instance for every wild battle (the templates in `_encounterTable` are never battled directly).
3. `MonsterFactory.Tame(WildMonster)` — called from the capture flow in
   `GameManager.HandleItemInput()` to convert a caught `WildMonster` into a `TamedMonster` added
   to the player's `Party`.

**Show Me The Code:**
- File: `src/GameManager.cs`, `SetupRegions()` (line 127)
- File: `src/Region.cs`, `GetRandomEncounter()` (line 35), `CreateFreshEncounter()` (line 46)
- File: `src/MonsterFactory.cs`, `Tame()` (line 8)
- File: `src/GameManager.cs`, `HandleItemInput()` (line 363) — capture branch calls `MonsterFactory.Tame(wild)`

---

### Q24. Explain the Strategy pattern as used for `Trainer.ChooseAction()`.

**Expected Answer:**
`Trainer` declares `public abstract TrainerAction ChooseAction(BattleState state);` — this is the
**Strategy interface**. `PlayerTrainer.ChooseAction` and `AITrainer.ChooseAction` are two
**concrete strategies**:
- `PlayerTrainer`: trivially returns `TrainerAction.UseMove` (decision already made via keyboard
  input setting `SelectedMoveIndex`).
- `AITrainer`: loops `active.MoveSet`, computes
  `TypeChart.GetMultiplier(move.MoveType, state.PlayerMonster.PrimaryType)` (multiplied by the
  secondary-type multiplier if the player's monster is dual-type), and keeps the **first** move
  with a strictly-higher multiplier than seen so far, storing the result in `_selectedMoveIndex`.

`BattleManager.EnemyTurn()` calls `_opponent.ChooseAction(state)` without any `is AITrainer`
check — the algorithm "what move do I pick" is swapped out per-trainer-type at runtime. A future
"healer AI" that prefers `HealMove` below 30% HP would only need a new `Trainer` subclass —
`BattleManager` doesn't change.

**Show Me The Code:**
- File: `src/Trainer.cs`, abstract `ChooseAction()` (line 33)
- File: `src/ConcreteTrainers.cs`, `PlayerTrainer.ChooseAction()` (line 51), `AITrainer.ChooseAction()` (lines 74–106)
- File: `src/BattleManager.cs`, call site in `EnemyTurn()` (line 158), `BattleState` constructed at line 157

---

### Q25. Explain the Observer pattern (the bonus 4th pattern) as used for battle animations.

**Expected Answer:**
`BattleManager` declares 6 C# events:
```csharp
public event Action<Monster, int>?                  AttackHit;
public event Action<Monster, int>?                  HealUsed;
public event Action<Monster, StatusCondition>?      StatusApplied;
public event Action<Monster, StatusCondition, int>? StatusDamage;
public event Action<Monster, StatusCondition>?      TurnSkipped;
public event Action<Monster, StatusCondition>?      StatusRemoved;
```
`BattleManager` is the **subject** — it fires these events (e.g.
`AttackHit?.Invoke(defender, damageDealt)`) without knowing or caring who's listening.
`GameManager` is the **observer** — in `StartWildBattle`/`StartTrainerBattle`/`StartBossBattle`
it subscribes (`+=`) to `AttackHit`, `HealUsed`, `StatusApplied`, `TurnSkipped`, and the handlers
(`OnAttackHit`, etc.) push new `BattleAnimation` objects into `_battleAnimations`. This decouples
battle *logic* from battle *visuals* completely — `BattleManager` could run with zero
subscribers and behave identically (just no animations would spawn).

**Note:** `StatusDamage` and `StatusRemoved` are declared and fired but **never subscribed to** —
burn/poison damage and status-clearing are logged as text only, with no animation. If asked, be
honest about this — it's a known gap, not a bug that breaks gameplay.

**Show Me The Code:**
- File: `src/BattleManager.cs`, event declarations (lines 19–24), e.g. fired at line 113 (`AttackHit?.Invoke(...)`) and line 207
- File: `src/GameManager.cs`, subscriptions in `StartWildBattle()` (lines 648–651), handlers `OnAttackHit` (line 763), `OnHealUsed` (line 772), `OnStatusApplied` (line 782), `OnTurnSkipped` (line 790)

---

## 5. Battle System

### Q26. Walk me through one full player turn, end-to-end.

**Expected Answer:**
1. Player presses arrow keys to set `_player.SelectedMoveIndex`, then **Enter** →
   `HandleBattleInput()` calls `_battleManager.PlayerTurn(_player.SelectedMoveIndex)`.
2. `PlayerTurn`: gets `attacker = _player.GetActiveMonster()`, `defender = EnemyMonster`.
3. `AddTurnSeparatorIfNew()` — adds a `"-- Turn N --"` log line if not already added this turn.
4. `ApplyStatusEffect(attacker)` — applies burn (10% maxHP) or poison (8% maxHP) damage *before*
   the move, regardless of what happens next.
5. If `attacker.Status` is `Frozen` or `Paralysed` → log a skip message, clear status to `None`,
   fire `TurnSkipped`/`StatusRemoved`, **return early** — the move is never executed.
6. Otherwise, look up `attacker.MoveSet[moveIndex]` and call `move.Execute(attacker, defender)`
   polymorphically. Compute `damageDealt`/`healAmount` by comparing HP before/after, fire
   `AttackHit`/`HealUsed`/`StatusApplied` as appropriate, and log the result.
7. Back in `GameManager`, if `IsBattleOver()` is false, set `_pendingEnemyTurn = true` with a
   150-frame `_interTurnDelay`. Once it expires, `Update()` calls `_battleManager.EnemyTurn()`
   (mirror-image pipeline), then `IsBattleOver(isEndOfRound: true)` (which also increments
   `_turnNumber`).

**Show Me The Code:**
- File: `src/BattleManager.cs`, `PlayerTurn()` (lines 57–119), `EnemyTurn()` (lines 122–213)
- File: `src/GameManager.cs`, `HandleBattleInput()` (line 304), `Update()` inter-turn logic (lines 538–549)

---

### Q27. What is the exact damage formula, and what factors affect it?

**Expected Answer:**
```
multiplier = TypeChart.GetMultiplier(move.MoveType, target.PrimaryType)
if target.HasDualType:
    multiplier *= TypeChart.GetMultiplier(move.MoveType, target.SecondaryType)

baseDamage  = (move.Power + caster.Level * 2) * caster.AttackMultiplier
finalDamage = (int)(baseDamage * multiplier)
target.TakeDamage(finalDamage)
```
- `caster.AttackMultiplier` is `0.75f` if the caster is `Burned`, else `1.0f` — burn reduces the
  *attacker's own* output by 25%, it does **not** make the attacker take more damage.
- Dual-type multipliers **multiply together**, so the effective range is `0.0x` (immune on
  either axis) to `4.0x` (super-effective on both axes) — not capped at `2.0x`.
- The `"It's super effective!"`/`"not very effective"`/`"had no effect"` text only fires when the
  *combined* multiplier is **exactly** `2.0f`/`0.5f`/`0.0f` — a `4.0x` or `0.25x` result deals the
  correct (quadrupled/quartered) damage but prints **no** effectiveness phrase.

**Show Me The Code:**
- File: `src/ConcreteMoves.cs`, `AttackMove.Execute()` (lines 11–32)
- File: `src/Monster.cs`, `AttackMultiplier` property (line 52)

---

### Q28. How does the type-effectiveness chart work?

**Expected Answer:**
`TypeChart` is a `static class` holding a `static readonly float[8,8] _chart`. Rows = attacking
type, columns = defending type, in the order of the `MonsterType` enum:
`Normal, Fire, Water, Grass, Electric, Flying, Fighting, Steel`. `GetMultiplier(attack, defend)`
is a single `O(1)` array lookup: `_chart[(int)attack, (int)defend]`. This one table drives three
different systems: (1) `AttackMove.Execute` for actual damage, (2) `AITrainer.ChooseAction` and
the boss's inline AI for move selection, and (3) UI hints (`IsSuperEffective`,
`GetTypeEffectivenessHint`) that highlight super-effective moves in the move panel.

**Show Me The Code:**
- File: `src/TypeChart.cs`, field `_chart` (line 9), `GetMultiplier()` (line 22)
- File: `src/Enumerations.cs`, enum `MonsterType` (line 3)
- File: `src/GameManager.cs`, `IsSuperEffective()` (line 1600), `GetTypeEffectivenessHint()` (line 1608)

---

### Q29. Explain each status condition: how it's applied, its per-turn effect, and how it ends.

**Expected Answer:**

| Status | Applied by | Per-turn effect | Ends |
|---|---|---|---|
| `Burned` | `StatusMove.Execute` (always succeeds if target has no status) | `TakeDamage(maxHp * 0.10)` at start of afflicted monster's turn, **and** -25% of that monster's own attack damage via `AttackMultiplier` | Only via `StatusCure` item, capture, or fainting — no auto-clear |
| `Poisoned` | same | `TakeDamage(maxHp * 0.08)` | same as Burned |
| `Frozen` | `StatusMove.Execute` (always succeeds) | none | Exactly 1 skipped turn, then auto-clears to `None` |
| `Paralysed` | `StatusMove.Execute`, but only 50% chance to actually apply (else "resisted paralysis") | none | Exactly 1 skipped turn (always — no "may still move" roll), then auto-clears |

`StatusCondition` is a plain (non-`[Flags]`) enum — a monster can have **only one** status at a
time. `StatusMove.Execute()` returns early with "...already affected..." if `target.Status !=
None`.

**Show Me The Code:**
- File: `src/Enumerations.cs`, enum `StatusCondition` (line 35)
- File: `src/Monster.cs`, `ApplyStatusEffect()` (lines 81–96), `AttackMultiplier` (line 52)
- File: `src/ConcreteMoves.cs`, `StatusMove.Execute()` (lines 68–79)
- File: `src/BattleManager.cs`, frozen/paralysed skip logic in `PlayerTurn()` (lines 78–99) and `EnemyTurn()` (lines 172–193)

---

### Q30. How is a battle determined to be over, and how is the result decided?

**Expected Answer:**
`BattleManager.IsBattleOver(bool isEndOfRound = false)`:
1. If the player's active monster is `null` or not `IsAlive` → `_result = PlayerDied`, return
   `true`.
2. Else if `EnemyMonster` is `null` or not `IsAlive` → `_result = PlayerWon`, and call
   `winner.GainExperience(...)` on the player's active monster — `500` XP if `_bossTarget !=
   null`, else `50 * _turnNumber`.
3. Otherwise, if `isEndOfRound` is true, increment `_turnNumber` (rounds, not half-turns), and
   return `false`.

`GameManager` calls `IsBattleOver(false)` after `PlayerTurn` and `IsBattleOver(true)` after
`EnemyTurn`. If `true`, `ShowBattleResult()` builds the message and starts a 150-frame
`_pendingBattleEnd` timer before `EndBattle()` runs.

**Show Me The Code:**
- File: `src/BattleManager.cs`, `IsBattleOver()` (lines 237–257)
- File: `src/GameManager.cs`, `ShowBattleResult()` (line 798), `EndBattle()` (line 706)

---

### Q31. `EnemyMonster` is described as the "key piece of glue" in `BattleManager`. Explain it.

**Expected Answer:**
```csharp
public Monster? EnemyMonster =>
    (Monster?)_wildTarget ?? (Monster?)_bossTarget ?? _opponent.GetActiveMonster();
```
For any given battle, exactly one path is "live": `_wildTarget` is non-null for wild battles,
`_bossTarget` is non-null for the boss battle, and for trainer battles **both are null**, so it
falls through to `_opponent.GetActiveMonster()` (a `TamedMonster`). Every other piece of code —
`IsBattleOver`, `EnemyTurn`, all the `Draw*` methods in `GameManager` — uses this single property
and never has to ask "is this a wild/boss/trainer battle?" directly. This is what lets the same
`PlayerTurn`/`EnemyTurn` pipeline handle all three battle types uniformly.

**Show Me The Code:**
- File: `src/BattleManager.cs`, `EnemyMonster` property (lines 53–54), fields `_wildTarget`/`_bossTarget` (lines 14–15)

---

## 6. Monster System

### Q32. Describe the `Monster` class hierarchy and what's unique to each subclass.

**Expected Answer:**
`Monster` (abstract) holds `_name`, `_hp`, `_maxHp`, `_level`, `_primaryType`, `_secondaryType`,
`_hasDualType`, `_statusCondition`, plus shared methods `TakeDamage`, `RestoreHp`, `LevelUp`,
`ApplyStatusEffect`, and computed properties `IsAlive`, `HpPercent`, `AttackMultiplier`.

| Subclass | Extra fields | Extra behaviour |
|---|---|---|
| `TamedMonster` | `_battleCry`, `_description`, `_experience`, `_moveSet` | `GainExperience(amount)` |
| `WildMonster` | `_battleCry`, `_description`, `_catchRate`, `_moveSet` | none beyond base |
| `BossMonster` | `_battleCry`, `_phase`, `_totalPhases`, `_moveSet` | `UpdatePhase()`, phase-based `GetBattleCry()`/`GetDescription()` |

All three hold a `Move[]` (composition). `MonsterFactory.Tame(WildMonster)` converts a
`WildMonster` to a `TamedMonster`, copying name/HP/level/types/cry/description/moveset, but
**always resets `Experience` to 0** — a level-14 captured monster starts its tamed life at 0 XP,
not partway to its next level.

**Show Me The Code:**
- File: `src/Monster.cs`, class `Monster` (line 4)
- File: `src/ConcreteMonsters.cs`, `TamedMonster` (line 5), `WildMonster` (line 57), `BossMonster` (line 98)
- File: `src/MonsterFactory.cs`, `Tame()` (line 8)

---

### Q33. How does levelling up and experience gain work?

**Expected Answer:**
`TamedMonster.GainExperience(int amount)`:
```csharp
_experience += amount;
int threshold = Level * 100;
if (_experience >= threshold)
{
    _experience -= threshold;
    LevelUp();
}
```
The threshold scales with the current level (`Level * 100`), and any *overflow* XP carries over
to the next level (it's `-=`, not reset to 0) — though only **one** level-up happens per call,
even if `amount` is large enough for two. `Monster.LevelUp()` increments `_level` and adds `+10`
to both `_maxHp` and current `_hp`. XP is awarded only to the player's active monster, only on a
win, via `IsBattleOver()`.

**Show Me The Code:**
- File: `src/ConcreteMonsters.cs`, `GainExperience()` (lines 39–48)
- File: `src/Monster.cs`, `LevelUp()` (lines 72–78)
- File: `src/BattleManager.cs`, call site in `IsBattleOver()` (line 251)

---

### Q34. Show me where a specific monster (e.g., the starter, Kelabit) is defined, and what it contains.

**Expected Answer:**
`MonsterFactory.CreateKelabit(int level = 12)` is a static factory method returning a
`new TamedMonster(...)` with hard-coded name, HP, type(s), battle cry, description, and a
`Move[]` array built inline (e.g., an `AttackMove`, possibly a `HealMove`/`StatusMove`). The
player's starter is created via `MonsterFactory.CreateKelabit(level: 8)` in
`GameManager.SetupGame()`. Each of the 7 species (Kelabit, Bekantan, Mawas, Rajah, Enggang,
Pelanduk, Temaju, Buaya) plus the boss (Naga Belian) has its own `CreateXxx` method following the
same shape.

**Show Me The Code:**
- File: `src/MonsterFactory.cs`, `CreateKelabit()` (line 23)
- File: `src/GameManager.cs`, `SetupGame()` (line 114) — `MonsterFactory.CreateKelabit(level: 8)`

---

## 7. Capture System

### Q35. Explain the capture probability formula and its edge cases.

**Expected Answer:**
```
catchChance = (1 - target.HpPercent) * target.CatchRate * orb._captureModifier
roll = Random.NextDouble()   // [0.0, 1.0)

roll <= catchChance        → Caught
roll <= catchChance * 1.5  → Escaped   ("almost" worked)
else                        → Failed   (orb missed entirely)
```
- The **only** lever the player controls is lowering the wild monster's HP — `CatchRate` is
  fixed per species (e.g., `0.30` for Buaya, `0.70` for Pelanduk) and `_captureModifier` is fixed
  per orb type (`1.0f` for the standard Capture Orb).
- At **full HP**, `catchChance = 0`, so `Caught` is impossible, and `Escaped` requires `roll <=
  0` — also impossible (roll is in `[0,1)`, but `<= 0` only matches `roll == 0.0` exactly, an
  effectively-zero-probability event). A full-HP monster can therefore only ever produce
  `Failed`.
- `ComputeCatchChance(WildMonster)` is the formula *without* the random roll — it's reused by the
  UI to show a Very Low/Low/Moderate/High/Very High label before the player commits to throwing
  the orb.

**Show Me The Code:**
- File: `src/ConcreteItems.cs`, `CaptureOrb.ComputeCatchChance()` (line 25), `AttemptCapture()` (lines 28–37)
- File: `src/Enumerations.cs`, enum `CaptureResult` (line 44)
- File: `src/BattleManager.cs`, `AttemptCapture(CaptureOrb orb)` (lines 221–233)

---

### Q36. Walk through what happens when a monster is successfully caught.

**Expected Answer:**
1. Player presses `C` (item menu) during a wild battle, selects a `CaptureOrb`, presses Enter →
   `_battleManager.AttemptCapture(orb)` → `orb.AttemptCapture(_wildTarget)` rolls the formula
   above and returns a `CaptureResult`.
2. `BattleManager` logs the result (`"X was caught!"` / `"X broke free!"` / `"The orb missed!"`).
3. A `CaptureOrbAnimation` plays in `GameManager` (orb travels → expands → shows
   "Caught!"/"Escaped!").
4. If `Caught`: capture-screen data is snapshotted (name, level, types, HP at capture, computed
   catch-chance label). `MonsterFactory.Tame(wild)` builds a brand-new `TamedMonster` from the
   `WildMonster`'s stats and moveset (with `Experience` reset to 0). `_player.Party.AddMonster(tamed)`
   — if the party is already full (6/6), the monster is **not** added, and a
   "released — Party full" message is logged instead.
5. `_pendingCaptureEnd = true` with a 140-frame timer → `_state = GameState.CaptureScreen`.
   `DrawCaptureScreen()` shows the captured monster's card; **Enter** → `EndBattle()`.

**Show Me The Code:**
- File: `src/GameManager.cs`, `HandleItemInput()` (line 363) — capture branch
- File: `src/BattleManager.cs`, `AttemptCapture()` (line 221)
- File: `src/MonsterFactory.cs`, `Tame()` (line 8)
- File: `src/Party.cs`, `AddMonster()` (line 19)
- File: `src/GameManager.cs`, `DrawCaptureScreen()` (line 1944)

---

## 8. AI Behaviour

### Q37. How does `AITrainer.ChooseAction()` decide which move to use? What are its limitations?

**Expected Answer:**
```
bestMultiplier = -1
bestIndex = 0
for i in 0..MoveSet.Length:
    mult = TypeChart.GetMultiplier(move[i].MoveType, playerPrimaryType)
    if player.HasDualType:
        mult *= TypeChart.GetMultiplier(move[i].MoveType, playerSecondaryType)
    if mult > bestMultiplier:   // strict > — ties go to the earliest move
        bestMultiplier = mult
        bestIndex = i
_selectedMoveIndex = bestIndex
```
**Limitations (deliberate simplifications, good to acknowledge if asked):**
- Purely **defensive type-matching** — ignores the move's own `Power`, the AI's own type (no
  same-type-attack-bonus modelling), and both sides' current HP.
- Ties always resolve to the **earliest** move in the array (`>` not `>=`).
- `_difficulty` is stored on `AITrainer` but **not currently used** to vary behaviour — every
  `AITrainer` plays identically regardless of its difficulty value.

**Show Me The Code:**
- File: `src/ConcreteTrainers.cs`, `AITrainer.ChooseAction()` (lines 74–106)
- File: `src/TypeChart.cs`, `GetMultiplier()` (line 22)

---

### Q38. How does the wild monster's AI differ from the trainer AI and the boss AI?

**Expected Answer:**
Three completely different "AIs" exist in `BattleManager.EnemyTurn()`:
1. **Wild monster** (`_wildTarget != null`): `moveIndex = new Random().Next(moveset.Length)` —
   purely random, zero type reasoning.
2. **Boss** (`_bossTarget != null`): calls `_bossTarget.UpdatePhase()` first (recomputes
   `_phase` from `HpPercent` — phase 3 at ≤33%, phase 2 at ≤66%, else phase 1, which also changes
   `GetBattleCry()`), then runs the **same type-matchup loop algorithm as `AITrainer`**, but
   **inline** in `BattleManager` — not via `ChooseAction`.
3. **Regular trainer** (`AITrainer`): calls `_opponent.ChooseAction(state)` (the Strategy
   pattern), which runs the type-matchup loop and stores the result in `ai.SelectedMoveIndex`.

**Examiner trap:** a `new AITrainer("Naga Belian", 3)` *is* constructed in `StartBossBattle()`,
but its `Party` is empty — it exists only so `BattleManager`'s constructor (`Trainer opponent`)
has something non-null to hold. The boss's actual moves come from `_bossTarget.MoveSet`, and its
AI is the separate inline loop — **the boss does not use the Strategy pattern**, despite the
`AITrainer` object existing.

**Show Me The Code:**
- File: `src/BattleManager.cs`, `EnemyTurn()` branches (lines 132–164)
- File: `src/ConcreteMonsters.cs`, `BossMonster.UpdatePhase()` (lines 120–129), `GetBattleCry()` (lines 132–141)
- File: `src/GameManager.cs`, `StartBossBattle()` (line 689)

---

## 9. Inventory System

### Q39. Describe the `Party` and `Inventory` classes — capacity limits and key operations.

**Expected Answer:**
`Party` wraps a `List<TamedMonster>` with a hard cap `MaxSlots = 6` (a `private const int`).
- `AddMonster(monster)` returns `false` if already at 6 (caller must handle the failure — used in
  the capture flow to show "Party full").
- `GetActiveMonster()` returns the first monster where `IsAlive` is true, or `null` if the whole
  party is wiped.
- `IsWiped()` — true only if **every** monster is not alive.
- `SwapToFront(index)` removes the monster at `index` and re-inserts it at position 0 — this is
  how "switching" your active monster works; the active monster is *always* slot 0.
- An indexer `this[int index]` lets `GameManager`/`BattleManager` treat `Party` like an array for
  drawing the party screen / swap menu.

`Inventory` is structurally similar but for `Item`s, with a configurable `_maxSlots` (default
20). `GetAllItems()` returns the **raw `List<Item>` reference** (not a copy) — `GameManager` uses
LINQ on it directly, e.g. `.Where(i => isWild || !(i is CaptureOrb))` to hide Capture Orbs from
the item menu during trainer battles (you can't catch a trainer's monster).

**Show Me The Code:**
- File: `src/Party.cs`, `MaxSlots` (line 8), `AddMonster()` (line 19), `GetActiveMonster()` (line 31), `SwapToFront()` (line 40), `IsWiped()` (line 49)
- File: `src/Inventory.cs`, constructor (line 10), `AddItem()` (line 20), `GetAllItems()` (line 32)
- File: `src/GameManager.cs`, `HandleItemInput()` (line 363) — LINQ filter on `GetAllItems()`

---

### Q40. Explain the `Item` hierarchy — `CaptureOrb`, `Potion`, `StatusCure` — and the `Use(TamedMonster)` signature.

**Expected Answer:**
`Item` (abstract) holds `_name`, `_description`, `_rarity` and declares
`Use(TamedMonster target)` and `GetDescription()` as abstract.
- `Potion.Use()` calls `target.RestoreHp(_healAmount)`.
- `StatusCure.Use()` clears `target.Status` to `None`, but **only if** `target.Status == _cures`
  exactly (a Burn Cure does nothing to a Poisoned monster).
- `CaptureOrb.Use()` is **deliberately empty** — a comment in the code explains the real catch
  logic lives in the separate method `AttemptCapture(WildMonster)`, called directly by
  `BattleManager`, not via the polymorphic `Use()`. `ComputeCatchChance(WildMonster)` is a
  *separate* public method so the UI can preview catch odds without rolling.

**Why `Use(TamedMonster target)` and not `Use(Monster target)`?** Items can only ever be used on
the player's **own party** — you can never Potion the enemy's monster — so the signature is
intentionally narrower than `Move.Execute(Monster, Monster)`, which operates on either side.

**Show Me The Code:**
- File: `src/Item.cs`, abstract class `Item` (line 4), abstract `Use()`/`GetDescription()` (lines 23–24)
- File: `src/ConcreteItems.cs`, `CaptureOrb` (line 7, `Use()` at line 19), `Potion` (line 48, `Use()` at line 59), `StatusCure` (line 72, `Use()` at line 83)

---

## 10. Region Progression & Event System

### Q41. Explain how regions progress and how "clearing" a region works.

**Expected Answer:**
Each `Region` has a `List<WildMonster>` (encounter templates), a `List<AITrainer>`, and an
`_isCleared` bool toggled by `Clear()`. A region can be cleared via **two independent paths**,
both calling the same idempotent `Clear()`:
1. **Walking onto the `RegionExit` tile** → `OverworldMap` starts a fade-to-black, then sets
   `RegionExitTriggered = true` → `GameManager.AdvanceRegion()` calls
   `_regions[_currentRegionIndex].Clear()`, then either `LoadOverworldMap(++_currentRegionIndex)`
   for the next region, or `StartBossBattle()` if this was the last region.
2. **Defeating the last un-wiped trainer** → `GameManager.EndBattle()`, on `PlayerWon` against a
   trainer, marks the NPC defeated (`_overworldMap.MarkTrainerDefeated`), then loops
   `_regions[_currentRegionIndex].Trainers`; if **all** parties are `IsWiped()`, calls
   `Region.Clear()` and `_overworldMap.SetCleared(true)` (which makes the exit tile render as a
   pulsing yellow "EXIT" and shows a "REGION CLEARED!" HUD badge).

This dual-path design is intentional — e.g., if a trainer was already wiped on a prior visit, the
player can still trigger the "cleared" state by walking to the exit.

**Show Me The Code:**
- File: `src/Region.cs`, `Clear()` (line 71), `IsCleared` (line 23)
- File: `src/GameManager.cs`, `AdvanceRegion()` (line 674), `EndBattle()` region-clear block (lines 730–738)
- File: `src/OverworldMap.cs`, `SetCleared()` (line 157), `MarkTrainerDefeated()` (line 161)

---

### Q42. How does the `_shopCatalog` factory-delegate pattern work?

**Expected Answer:**
`GameManager` has a private field `_shopCatalog`, an array of tuples
`(string Name, int Cost, Func<Item> Create)`. The `Func<Item> Create` is a **factory delegate** —
when the player buys an item in `HandleShopInput()`, the code calls `create()` to produce a
**brand-new** `Item` instance. This matters because `Item`s are reference types with their own
state (e.g., a `Potion`'s description is built from `_healAmount` at construction) — if the
catalog stored shared `Item` instances instead of factories, two purchases of "Potion" would add
the *same object reference* twice to the inventory, which would be incorrect if items ever held
mutable per-instance state.

**Show Me The Code:**
- File: `src/GameManager.cs`, field `_shopCatalog` (search near line 85–110 region), `HandleShopInput()` (line 503), `DrawShop()` (line 955)

---

## 11. Design Decisions, Strengths, Weaknesses & Improvements

### Q43. What are the project's strongest design decisions, and why?

**Expected Answer:**
1. **`EnemyMonster` null-coalescing property** (`BattleManager`, line 53) — collapses three
   battle types (wild/boss/trainer) into one code path for `IsBattleOver`/`EnemyTurn`/all
   drawing, avoiding `if (isWild) ... else if (isBoss) ... else ...` everywhere.
2. **Strict separation of domain logic from SplashKit** — `Monster`, `Move`, `Item`, `Trainer`,
   `BattleManager`, `TypeChart` have zero engine dependencies, making the core rules testable and
   easy to reason about in isolation.
3. **`Trainer.ChooseAction(BattleState)` Strategy pattern** decouples "who decides the move" from
   "how the battle is run" — `BattleManager` is agnostic to whether it's facing a human or AI.
4. **Factory methods for monster construction** — centralizes the ~7-argument construction
   recipe per species, so the rest of the codebase never repeats it.
5. **Frame-based timers (`_interTurnDelay`, `_battleResultTimer`, `_captureEndTimer`)** give the
   turn-based battle a paced, readable feel without blocking the game loop with `Thread.Sleep`.

**Show Me The Code:**
- `src/BattleManager.cs` line 53 (`EnemyMonster`)
- `src/Trainer.cs` line 33 (`ChooseAction`)
- `src/MonsterFactory.cs` (whole file)
- `src/GameManager.cs` lines 66–82 (timer fields)

---

### Q44. What are the known weaknesses or "honest gaps" in the project — things an examiner might probe?

**Expected Answer:**
1. **`GameState.Victory` is dead code.** It's declared in `Enumerations.cs`, `DrawVictory()`
   exists and is wired into the `Draw()` switch, and `HandleInput` even has a
   `case GameState.Victory:` — but **nothing in `EndBattle()` ever sets `_state =
   GameState.Victory`**, even after defeating the final boss. The win overlay shows via
   `ShowBattleResult()`, then `EndBattle()` sends the player to `Overworld` like any normal win.
2. **The boss does not use the Strategy pattern**, despite a `new AITrainer("Naga Belian", 3)`
   being constructed — its `Party` is empty and unused; the real boss AI is a separate inline
   loop in `BattleManager.EnemyTurn()`.
3. **`StatusDamage` and `StatusRemoved` events have zero subscribers** — burn/poison
   damage and status-clearing are logged as text but produce no `BattleAnimation`.
4. **`AITrainer._difficulty` is stored but never used** to vary AI behaviour.
5. **Effectiveness text only matches exactly `2.0f`/`0.5f`/`0.0f`** — a `4.0x` (double
   super-effective, dual-type) or `0.25x` result deals correct damage but shows no phrase.
6. **AI move selection ignores move `Power`, STAB, and HP** — purely defensive type-matching,
   with ties resolved to the lowest index.
7. **`MonsterFactory.Tame()` always resets `Experience` to 0**, regardless of the wild monster's
   level — a high-level capture doesn't carry over partial progress.

**Show Me The Code:**
- `src/Enumerations.cs` line 23 (`GameState.Victory`), `src/GameManager.cs` `DrawVictory()` (line 2017), `EndBattle()` (line 706 — note no `Victory` assignment)
- `src/GameManager.cs` `StartBossBattle()` (line 689)
- `src/BattleManager.cs` lines 22, 24 (`StatusDamage`, `StatusRemoved` declarations)
- `src/ConcreteTrainers.cs` `AITrainer` field `_difficulty` (line 61)
- `src/ConcreteMoves.cs` `AttackMove.Execute()` switch expression (lines 23–29)

---

### Q45. If you had more time, what would you improve or add next?

**Expected Answer (suggest 3–4, tailored to your own priorities):**
1. **Wire up `GameState.Victory`** as a proper end-game screen after defeating Naga Belian
   (credits, stats summary), instead of falling back to `Overworld`.
2. **Use `AITrainer._difficulty`** to vary behaviour — e.g., higher difficulty could weight move
   choice by `Power * multiplier` instead of pure type-matching, or occasionally use `HealMove`
   when below a HP threshold (this would also bring the boss closer to a true Strategy-pattern
   user if it adopted the same mechanism).
3. **Subscribe to `StatusDamage`/`StatusRemoved`** and add corresponding `BattleAnimation`s (e.g.
   a damage-number pop for burn/poison ticks, a small "status cured" sparkle).
4. **Add a combined-multiplier-aware effectiveness message** (e.g. "It's extremely effective!"
   for `4.0x`, "barely affects" for `0.25x`) so the UI text matches the actual damage dealt for
   dual-type matchups.
5. **Carry over a fraction of a wild monster's level-based XP on capture**, so high-level catches
   feel rewarding rather than resetting to 0 XP.

**Show Me The Code (where each change would land):**
- Victory screen: `src/GameManager.cs` `EndBattle()` (line 706), `DrawVictory()` (line 2017)
- AI difficulty: `src/ConcreteTrainers.cs` `AITrainer.ChooseAction()` (line 74)
- Status animations: `src/GameManager.cs` `StartWildBattle()`/`StartTrainerBattle()` subscriptions (lines 648–651), new `On...` handlers near `OnAttackHit` (line 763)
- Effectiveness text: `src/ConcreteMoves.cs` `AttackMove.Execute()` (lines 23–29)
- XP carryover: `src/MonsterFactory.cs` `Tame()` (line 8)

---

## Quick Navigation Index

| Topic | Primary File | Primary Class/Method |
|---|---|---|
| Singleton | `src/GameManager.cs` | `GameManager.GetInstance()` (line 102) |
| Factory | `src/MonsterFactory.cs` | `MonsterFactory.CreateXxx()` / `Tame()` (line 8) |
| Strategy | `src/Trainer.cs`, `src/ConcreteTrainers.cs` | `ChooseAction()` (line 33 / 51 / 74) |
| Observer | `src/BattleManager.cs`, `src/GameManager.cs` | events (lines 19–24), `OnAttackHit` etc. (line 763) |
| Abstraction | `src/Monster.cs`, `src/Move.cs`, `src/Item.cs`, `src/Trainer.cs` | abstract methods |
| Encapsulation | `src/Monster.cs` | `TakeDamage()` (line 60), properties |
| Inheritance | `src/ConcreteMonsters.cs`, `src/ConcreteMoves.cs`, `src/ConcreteTrainers.cs` | `: Monster`/`: Move`/`: Trainer` |
| Polymorphism | `src/BattleManager.cs` | `selectedMove.Execute()` (line 110), `ChooseAction()` (line 158) |
| Damage formula | `src/ConcreteMoves.cs` | `AttackMove.Execute()` (line 11) |
| Type chart | `src/TypeChart.cs` | `GetMultiplier()` (line 22) |
| Capture formula | `src/ConcreteItems.cs` | `CaptureOrb.AttemptCapture()` (line 28) |
| Status effects | `src/Monster.cs`, `src/BattleManager.cs` | `ApplyStatusEffect()` (line 81) |
| Region progression | `src/Region.cs`, `src/GameManager.cs` | `Clear()` (line 71), `AdvanceRegion()` (line 674) |
| AI move selection | `src/ConcreteTrainers.cs`, `src/BattleManager.cs` | `AITrainer.ChooseAction()` (line 74), boss loop (line 144–151) |
