# Project Knowledge Guide — Viva Preparation

This document is a code-level walkthrough of **Monster Tamer: Guardians of Sarawak**, written so you
can explain *every part of your own codebase* confidently in a viva. It does not cover UML — only
the actual C# implementation in `src/`.

---

## 1. Overall Architecture

### 1.1 The four "layers" of the game

| Layer | Files | Responsibility |
|---|---|---|
| **Domain model** | `Monster.cs`, `ConcreteMonsters.cs`, `Move.cs`, `ConcreteMoves.cs`, `Item.cs`, `ConcreteItems.cs`, `Trainer.cs`, `ConcreteTrainers.cs`, `TypeChart.cs`, `Enumerations.cs` | The "rules of the world" — stats, damage, types, status effects. No SplashKit calls anywhere in this layer. |
| **Containers / data** | `Party.cs`, `Inventory.cs`, `Region.cs`, `BattleState.cs`, `LogEntry.cs` | Hold collections of domain objects (a trainer's 6 monsters, a bag of items, a region's encounter table). |
| **Game logic / orchestration** | `BattleManager.cs`, `MonsterFactory.cs` | Runs a single battle turn-by-turn; builds fully-configured monsters. |
| **Presentation / engine glue** | `GameManager.cs`, `OverworldMap.cs`, `BattleAnimation.cs`, `Program.cs` | SplashKit window, game loop, input handling, all `Draw()` calls, the tile-based overworld. |

This separation is the backbone of the "Battle logic must not be in GameManager" and "Drawing logic
must not be in game logic classes" rules from `CLAUDE.md`. If you're asked *"why is BattleManager a
separate class from GameManager?"* — the answer is **separation of concerns**: BattleManager knows
nothing about pixels, windows, or SplashKit; GameManager knows nothing about how damage is
calculated.

### 1.2 Control flow — who calls who

```
Program.Main()
  └─ GameManager.GetInstance()   ← Singleton, builds everything
        └─ .Run()                ← the game loop
              ├─ SplashKit.ProcessEvents()
              ├─ HandleInput()    ← reads keyboard, mutates state
              ├─ Update()         ← advances animations, AI timers, overworld movement
              └─ Draw()           ← one giant switch on GameState
```

`GameManager` is the **only** class that talks to `SplashKit.Window`, `SplashKit.KeyTyped`, etc.
(except `OverworldMap` and `BattleAnimation`, which are explicitly allowed to draw per `CLAUDE.md`).

### 1.3 How GameManager coordinates everything

`GameManager` holds references to (almost) every other major system as private fields:

```csharp
private PlayerTrainer   _player;          // the one player, owns Party + Inventory
private BattleManager?  _battleManager;   // null outside battle; created fresh per battle
private List<Region>    _regions;         // the 3 regions, in order
private int             _currentRegionIndex;
private OverworldMap?   _overworldMap;    // null only before SetupGame() finishes
```

It is essentially a **state machine** driven by the `GameState` enum
(`MainMenu`, `Overworld`, `EncounterPrompt`, `Battle`, `PartyScreen`, `CaptureScreen`, `Victory`,
`GameOver`). Every method in `HandleInput()`, `Update()`, and `Draw()` branches on `_state`, and
transitions between states are made by simply assigning `_state = GameState.X`.

GameManager's three jobs every frame:

1. **HandleInput()** — translate raw key presses into either (a) direct state mutation (e.g. move
   the cursor in a menu) or (b) calls into `BattleManager` / `OverworldMap` / `Party` / `Inventory`.
2. **Update()** — advance anything time-based: HP bar lerp animations, `BattleAnimation` objects,
   the inter-turn delay before the enemy acts, the overworld player's movement and encounter rolls.
3. **Draw()** — pure rendering, dispatched by `_state`.

GameManager never calculates damage, type multipliers, or capture odds itself — it always delegates
to `BattleManager`, `Move.Execute()`, `TypeChart`, or `CaptureOrb.AttemptCapture()`.

---

## 2. Core Gameplay Flow

### 2.1 Startup flow

1. `Program.Main()` calls `GameManager.GetInstance()`.
2. Because `_instance` is `null`, the **private constructor** runs:
   - Creates a `1280x720` `Window`, toggles fullscreen.
   - `_state = GameState.MainMenu`.
   - `_player = new PlayerTrainer("Ranger")`.
   - Calls `SetupGame()`.
3. `SetupGame()`:
   - `MonsterFactory.CreateKelabit(level: 8)` → added to `_player.Party` (this is the starter).
   - Adds 2 Potions + 3 Capture Orbs to `_player.Bag`.
   - Calls `SetupRegions()` — builds the 3 `Region` objects (Rainforest, River Delta, Ancient
     Highlands), each with an encounter table (`WildMonster` templates) and an `AITrainer` with
     their own `Party` of `TamedMonster`s.
   - Calls `LoadOverworldMap(0)` — builds `TrainerNPC` objects for region 0's trainers and asks
     `MapFactory.CreateRainforestMap()` for the tile grid.
4. Back in `GetInstance()`, `.Run()` is called:
   - Creates/starts the `"blink"` SplashKit timer (used for star twinkle, EXIT pulse, sprite bob).
   - `LoadMonsterSprites()` — loads every monster + overworld PNG into `_monsterSprites`.
   - `LoadBgm()` — loads `overworld.ogg` and `battle.ogg`.
   - Enters the `while (!_window.CloseRequested)` loop.
5. Player is shown `DrawMainMenu()`. Pressing **Enter/Space** sets `_state = Overworld` and starts
   the overworld BGM.

### 2.2 Exploration flow (Overworld)

- `Update()` reads WASD **and** arrow keys (`KeyDown` for smooth movement) and calls
  `_overworldMap.Update(up, down, left, right)`.
- Inside `OverworldMap.Update()`:
  - `TryMove(dx, dy)` — computes the tile the player *would* enter and only moves if
    `Tile.Walkable` is true (false for `Tree`, `Water`, `House`).
  - `CheckTriggers()` — looks at the tile the player is now standing on:
    - **`TallGrass`** + new tile + cooldown clear → 15% roll → `WildEncounterTriggered = true`.
    - **`RegionExit`** → starts a fade-to-black transition (`_exitFading`); only sets
      `RegionExitTriggered = true` once the fade reaches full black.
    - **`Shop`** + new tile → `ShopEncounterTriggered = true`.
    - **Trainer proximity** (distance < 1.5 tiles, not yet `Defeated`, cooldown clear) →
      `TrainerEncounterTriggered = true` and `TriggeredTrainer` is set.
- Back in `GameManager.Update()`, these flags are read **once** and converted into state
  transitions:
  - Wild encounter → `_pendingEncounter = encounter; _state = EncounterPrompt`.
  - Trainer encounter → `_pendingTrainerEncounter = npc.Trainer; _state = EncounterPrompt`.
  - Region exit → `AdvanceRegion()`.
  - Shop → `_showingShop = true`.
- `EncounterPrompt` shows a card (`DrawWildEncounterCard` / `DrawTrainerEncounterCard`).
  **Enter** → `StartWildBattle()` / `StartTrainerBattle()`. **R or Esc** → cancel, return to
  Overworld, and call `_overworldMap.ApplyFleeingCooldown()` (5-second cooldown so you don't
  instantly re-trigger the same tile).

### 2.3 Battle flow

1. `StartWildBattle` / `StartTrainerBattle` / `StartBossBattle` all do the same three things:
   - `_battleManager = new BattleManager(_player, opponent)` — fresh instance every battle.
   - Subscribe to `BattleManager`'s events (`AttackHit`, `HealUsed`, `StatusApplied`,
     `TurnSkipped`) — these drive `BattleAnimation` objects.
   - Seed the battle log with an intro line, reset animation state, switch BGM to `"battle"`,
     `_state = Battle`.
2. **Player's turn** (`HandleBattleInput`):
   - Arrow keys move `_player.SelectedMoveIndex` (clamped 0..3, with the 2x2 grid using
     `±1`/`±2`).
   - `C` → item menu (`_showingItemMenu`), `X` → swap menu (`_showingSwapMenu`).
   - **Enter** → `_battleManager.PlayerTurn(_player.SelectedMoveIndex)`.
3. `BattleManager.PlayerTurn()`:
   - Adds a `"-- Turn N --"` separator if not already added this turn.
   - Applies burn/poison damage to the attacker (`ApplyStatusEffect`).
   - If `Frozen` or `Paralysed`, the turn is **skipped entirely** (status then clears to `None`)
     and the method returns early.
   - Otherwise, looks up `attacker.MoveSet[moveIndex]` and calls `move.Execute(attacker, defender)`
     **polymorphically** — `AttackMove`, `HealMove`, and `StatusMove` each do something different.
   - Logs the result, fires `AttackHit`/`HealUsed`/`StatusApplied` events for animations.
4. Back in `GameManager`, if `IsBattleOver()` is false, `_pendingEnemyTurn = true` with a
   150-frame delay (`_interTurnDelay`) so the player can read the log before the enemy acts.
5. Once the delay expires, `Update()` calls `_battleManager.EnemyTurn()`:
   - **Wild monster** → picks a **random** move from `_wildTarget.MoveSet`.
   - **Boss** → calls `UpdatePhase()`, then picks the move with the best type multiplier against
     the player's monster (inline logic, *not* via `ChooseAction`).
   - **AITrainer** → calls `_opponent.ChooseAction(state)` (Strategy Pattern), which sets
     `ai.SelectedMoveIndex` to the best type-advantage move.
   - Same status-effect / frozen / paralysed / Execute() pipeline as `PlayerTurn`.
6. After `EnemyTurn()`, `IsBattleOver(isEndOfRound: true)` is checked — this is also where
   `_turnNumber++` happens (only once per full round).
7. `IsBattleOver()` sets `BattleResult`:
   - Player's active monster fainted → `PlayerDied`.
   - Enemy's active monster (or wild/boss) fainted → `PlayerWon`, and
     `winner.GainExperience(...)` is called on the player's active monster.
   - Otherwise → `Ongoing`.
8. `ShowBattleResult()` builds the result message and starts a 150-frame
   `_pendingBattleEnd` timer; the screen fades to a result overlay. **Enter** → `EndBattle()`.

### 2.4 Capture flow

1. Player presses `C` (item menu) during a **wild** battle, selects a `CaptureOrb`, presses
   Enter.
2. `_battleManager.AttemptCapture(orb)` → `orb.AttemptCapture(_wildTarget)`:
   ```
   catchChance = (1 - wild.HpPercent) * wild.CatchRate * orb._captureModifier
   roll = Random.NextDouble()
   roll <= catchChance        → Caught
   roll <= catchChance * 1.5  → Escaped
   else                        → Failed
   ```
3. A `CaptureOrbAnimation` plays (orb travels → expands → shows "Caught!"/"Escaped!").
4. If **Caught**:
   - Capture-screen data is snapshotted (name, level, types, HP at capture, computed catch-chance
     label).
   - `MonsterFactory.Tame(wild)` builds a brand-new `TamedMonster` from the `WildMonster`'s stats
     and moveset.
   - `_player.Party.AddMonster(tamed)` — if the party is full (6/6), the monster is **not**
     added and a "released — Party full" message is logged instead.
   - `_pendingCaptureEnd = true` with a 140-frame timer → `_state = CaptureScreen`.
5. `DrawCaptureScreen()` shows the captured monster's card. **Enter** → `EndBattle()`.

### 2.5 Region progression flow

- Each `Region` tracks `_isCleared` and a list of `AITrainer`s.
- **`AdvanceRegion()`** is called when the player walks onto a `RegionExit` tile (after the fade):
  - `_regions[_currentRegionIndex].Clear()` (sets `_isCleared = true`).
  - If there's a next region → `_currentRegionIndex++` and `LoadOverworldMap(...)`.
  - Otherwise (this was the last region) → `StartBossBattle()` (Naga Belian).
- **`EndBattle()`**, on `PlayerWon` against a trainer:
  - `_overworldMap.MarkTrainerDefeated(_lastTrainerBattled)` — that NPC's `!` icon disappears.
  - Loops `_regions[_currentRegionIndex].Trainers`; if **all** trainers' parties are wiped →
    `_regions[_currentRegionIndex].Clear()` and `_overworldMap.SetCleared(true)`.
  - `SetCleared(true)` makes the `RegionExit` tile render as a pulsing yellow "EXIT" instead of a
    plain locked-looking tile, and updates the HUD with a "REGION CLEARED!" badge.
- A region can therefore be "cleared" by **either** path — defeating its trainers, or simply
  walking to the exit (e.g. if a trainer was already wiped from a previous visit). Both paths call
  the same idempotent `Region.Clear()`.

---

## 3. Every Major Class

### 3.1 `Monster` (abstract) — [Monster.cs](../src/Monster.cs)

**Responsibility:** the shared "creature" contract — HP, level, type(s), status condition, and the
universal combat operations (`TakeDamage`, `RestoreHp`, `LevelUp`, `ApplyStatusEffect`).

**Key fields (all `private`):** `_name`, `_hp`, `_maxHp`, `_level`, `_primaryType`,
`_secondaryType`, `_hasDualType`, `_statusCondition`.

**Key methods:**
- `TakeDamage(int)` / `RestoreHp(int)` — clamp HP to `[0, MaxHp]`.
- `LevelUp()` — `_level++`, `+10` to both `MaxHp` and current `Hp`.
- `ApplyStatusEffect()` — applies burn (10% maxHP) or poison (8% maxHP) damage and **returns the
  damage dealt** (0 for non-damaging statuses); used by `BattleManager` to log the exact number.
- `AttackMultiplier` (computed property) — `0.75f` if `Burned`, else `1.0f`. Used by
  `AttackMove.Execute()` to weaken the burned attacker's own damage output.

**Abstract methods:** `GetBattleCry()`, `GetDescription()` — every subclass must supply flavour
text; this is the textbook **Polymorphism** hook.

**Why it exists:** lets `BattleManager`, `TypeChart`, and all `Move` subclasses operate on a single
`Monster` reference without caring whether it's a player's `TamedMonster`, a `WildMonster`, or the
`BossMonster`.

### 3.2 `TamedMonster`, `WildMonster`, `BossMonster` — [ConcreteMonsters.cs](../src/ConcreteMonsters.cs)

| Class | Extra fields | Extra behaviour | Used for |
|---|---|---|---|
| `TamedMonster` | `_battleCry`, `_description`, `_experience`, `_moveSet` | `GainExperience(amount)` — accumulates XP, calls `LevelUp()` when `Experience >= Level * 100`, carrying over the remainder | Player's party + AI trainers' parties |
| `WildMonster` | `_battleCry`, `_description`, `_catchRate`, `_moveSet` | none beyond base | Encounter-table monsters |
| `BossMonster` | `_battleCry`, `_phase`, `_totalPhases`, `_moveSet` | `UpdatePhase()` recomputes `_phase` from `HpPercent` (66%/33% thresholds); `GetBattleCry()` returns a different roar per phase | Naga Belian only |

**Relationships:** all three hold a `Move[]` (their moveset) — this is a **composition**
relationship (a monster's moves are created with it and have no independent lifetime).
`MonsterFactory.Tame(WildMonster)` is the bridge that converts a `WildMonster` into a
`TamedMonster`, copying name/HP/level/types/cry/description/moveset across — note the new
`TamedMonster` always starts with `Experience = 0`, regardless of the wild monster's level.

### 3.3 `Move` (abstract) + `AttackMove`/`HealMove`/`StatusMove` — [Move.cs](../src/Move.cs), [ConcreteMoves.cs](../src/ConcreteMoves.cs)

**`Move`** holds `_name`, `_power`, `_manaCost`, `_moveType` and declares two abstract methods:
`Execute(Monster caster, Monster target) : string` and `GetMoveCategory() : string`.

- **`AttackMove`** (`manaCost = 10` fixed via base constructor):
  - Looks up `TypeChart.GetMultiplier(MoveType, target.PrimaryType)`, multiplies by the secondary
    type's multiplier if `target.HasDualType`.
  - `baseDamage = (Power + caster.Level * 2) * caster.AttackMultiplier`
  - `finalDamage = (int)(baseDamage * multiplier)`
  - Calls `target.TakeDamage(finalDamage)`, returns a string describing the hit and (if
    multiplier is exactly 2.0/0.5/0.0) an effectiveness phrase.
- **`HealMove`** (`manaCost = 15`): `amount = Power + caster.Level * 2`; calls
  `caster.RestoreHp(amount)` — **heals the caster, not the target**.
- **`StatusMove`** (`manaCost = 12`, `Power = 0`): stores `_inflicts` (a `StatusCondition`).
  - If the target already has a status, the move fails outright.
  - If `_inflicts == Paralysed`, there's an extra 50% chance to fail ("resisted paralysis").
  - Otherwise sets `target.Status = _inflicts`.

**Why three subclasses instead of one `Move` with an enum "category" field?** This is the
project's clearest demonstration of **Polymorphism** — `BattleManager` calls
`selectedMove.Execute(attacker, defender)` without an `if/else` on move type; each subclass
decides what "executing" means.

### 3.4 `Item` (abstract) + `CaptureOrb`/`Potion`/`StatusCure` — [Item.cs](../src/Item.cs), [ConcreteItems.cs](../src/ConcreteItems.cs)

**`Item`** holds `_name`, `_description`, `_rarity` and declares `Use(TamedMonster target)` and
`GetDescription()` as abstract.

- **`CaptureOrb`**: `Use()` is **deliberately empty** — a comment explains that the real catch
  logic lives in `AttemptCapture(WildMonster)` (called by `BattleManager`, not via `Use()`).
  `ComputeCatchChance(WildMonster)` is a separate public method so the UI (`DrawBattleTips`,
  capture screen) can preview the odds without actually rolling.
- **`Potion`**: `Use()` calls `target.RestoreHp(_healAmount)`.
- **`StatusCure`**: `Use()` clears `target.Status` **only if** it matches `_cures` exactly.

**Why `Use(TamedMonster target)` and not `Use(Monster target)`?** Items can only ever be used on
the player's own party (you can't Potion the enemy), so the signature is intentionally narrower
than `Move.Execute(Monster, Monster)`.

### 3.5 `Trainer` (abstract) + `PlayerTrainer`/`AITrainer` — [Trainer.cs](../src/Trainer.cs), [ConcreteTrainers.cs](../src/ConcreteTrainers.cs)

**`Trainer`** holds `_name`, `_party` (a `Party`), `_bag` (an `Inventory`) — both created in the
constructor (composition: a `Trainer` *owns* its `Party` and `Inventory`; they don't outlive it
and aren't shared).

`GetActiveMonster()` — concrete, shared by both subclasses — returns the **first alive**
`TamedMonster` in `_party` (or `null` if the whole party is fainted).

`ChooseAction(BattleState state) : TrainerAction` — **abstract**, the Strategy Pattern interface.

- **`PlayerTrainer`**: adds `_gold` (`AddGold`/`SpendGold`), `_selectedMoveIndex` (clamped to the
  active monster's `MoveSet.Length` whenever set), `_selectedPartyIndex`. Its `ChooseAction()`
  always returns `TrainerAction.UseMove` — the *real* decision was already made via keyboard input
  setting `SelectedMoveIndex`.
- **`AITrainer`**: adds `_difficulty` (stored but not currently used to vary behaviour) and
  `_selectedMoveIndex`. Its `ChooseAction()` loops `active.MoveSet`, computes the type multiplier
  of each move against `state.PlayerMonster`'s type(s), and keeps the **first** move with the
  strictly-highest multiplier.

### 3.6 `TypeChart` (static) — [TypeChart.cs](../src/TypeChart.cs)

A `static readonly float[8,8] _chart`. Rows = attacking type, columns = defending type, ordered to
match the `MonsterType` enum (`Normal, Fire, Water, Grass, Electric, Flying, Fighting, Steel`).
`GetMultiplier(attack, defend)` is a single array lookup: `_chart[(int)attack, (int)defend]`.
Every damage/AI calculation in the project funnels through this one method.

### 3.7 `BattleState` — [BattleState.cs](../src/BattleState.cs)

A tiny immutable (`{ get; }` only) snapshot: `PlayerMonster`, `EnemyMonster`, `TurnNumber`,
`IsPlayerTurn`. Its **only** purpose is to be passed into `Trainer.ChooseAction(BattleState)` so an
AI can "see" the battle without holding a reference to `BattleManager` itself (decoupling).

### 3.8 `Party` — [Party.cs](../src/Party.cs)

Wraps a `List<TamedMonster>` with a hard cap (`MaxSlots = 6`).
- `AddMonster` — fails (`return false`) if already at 6.
- `GetActiveMonster()` — first monster where `IsAlive` is true, else `null`.
- `IsWiped()` — true only if **every** monster's `IsAlive` is false.
- `SwapToFront(index)` — removes the monster at `index` and re-inserts it at position 0; this is
  how "switching" your active monster works (the active monster is *always* slot 0).
- Indexer `this[int index]` — lets `GameManager`/`BattleManager` treat `Party` like an array for
  drawing the party screen / swap menu.

### 3.9 `Inventory` — [Inventory.cs](../src/Inventory.cs)

Same shape as `Party` but for `Item`s, with a configurable `_maxSlots` (default 20).
`GetAllItems()` returns the raw `List<Item>` reference — used by `GameManager` with LINQ
(`.Where(i => isWild || !(i is CaptureOrb))`) to filter Capture Orbs out of trainer-battle item
menus.

### 3.10 `Region` — [Region.cs](../src/Region.cs)

Holds a `List<WildMonster>` (encounter **templates** — never battled directly) and a
`List<AITrainer>` (real, stateful trainers).

- `GetRandomEncounter()` — picks a random template, generates `level = template.Level + rng(-2,2)`
  (clamped to ≥1), then calls `CreateFreshEncounter(name, level)` which switches on the template's
  name and calls the matching `MonsterFactory.CreateXxx(level)`. **This is why every wild battle
  uses a brand-new `WildMonster` instance** — the templates in `_encounterTable` are never mutated
  or battled.
- `GetNextTrainer()` — first trainer whose `Party.IsWiped()` is false (not currently used by
  `GameManager`, but available for "next opponent" logic).
- `Clear()` — sets `_isCleared = true` (one-way flag).

### 3.11 `MonsterFactory` (static) — [MonsterFactory.cs](../src/MonsterFactory.cs)

One static method per Sarawak monster (`CreateKelabit`, `CreateBekantan`, ... `CreateNagaBelian`),
each hard-coding name, HP, level default, type(s), battle cry, description, and a hand-built
`Move[]`. Also provides `CreateXxxTamed(level)` helper wrappers (e.g. `CreateBekantanTamed`) that
call `CreateBekantan(level)` then `Tame(...)` — used when building **AI trainer** parties (they
need `TamedMonster`s, not `WildMonster`s).

`Tame(WildMonster monster)` is the single conversion point from "wild" to "tamed" — it picks the
correct `TamedMonster` constructor overload based on `monster.HasDualType`.

### 3.12 `BattleManager` — [BattleManager.cs](../src/BattleManager.cs)

**Responsibility:** run exactly one battle from start to finish. A new instance is created for
every battle (wild, trainer, or boss) — it is **not** a singleton and holds no state across
battles.

**Key fields:** `_player` (`PlayerTrainer`), `_opponent` (`Trainer` — base type!), `_result`
(`BattleResult`), `_turnNumber`, `_battleLog` (`List<LogEntry>`), `_wildTarget` (`WildMonster?`),
`_bossTarget` (`BossMonster?`).

**The `EnemyMonster` property** is the key piece of glue:
```csharp
public Monster? EnemyMonster =>
    (Monster?)_wildTarget ?? (Monster?)_bossTarget ?? _opponent.GetActiveMonster();
```
Exactly one of `_wildTarget`/`_bossTarget` is non-null for wild/boss battles; for trainer battles
both are `null` and it falls through to the opponent's active `TamedMonster`. Every other method
(`IsBattleOver`, `EnemyTurn`, all the `Draw*` methods in `GameManager`) uses this single property
and never needs to ask "is this a wild battle?" directly.

**Key methods:**
- `PlayerTurn(int moveIndex)` / `EnemyTurn()` — mirror image of each other; see §2.3 for the
  shared status-effect/skip/execute pipeline.
- `AttemptCapture(CaptureOrb)` — delegates to `orb.AttemptCapture(_wildTarget)`.
- `IsBattleOver(bool isEndOfRound)` — checks both sides for a wipe, sets `_result`, awards XP, and
  (only when `isEndOfRound`) increments `_turnNumber`.
- `ApplyStatusEffect(Monster)` — private helper; logs burn/poison damage and fires `StatusDamage`.
- `AddTurnSeparatorIfNew()` — idempotent per-turn-number log separator.

**Events (Observer Pattern — see §5.4):** `AttackHit`, `HealUsed`, `StatusApplied`,
`StatusDamage`, `TurnSkipped`, `StatusRemoved`. `GameManager` subscribes to 4 of these to spawn
`BattleAnimation`s.

### 3.13 `OverworldMap` + `OverworldPlayer` + `TrainerNPC` + `MapFactory` — [OverworldMap.cs](../src/OverworldMap.cs)

- **`Tile`** — wraps a `TileType` enum value; `Walkable` is computed (`true` unless `Tree`,
  `Water`, or `House`).
- **`OverworldPlayer`** — pixel position (`_x`, `_y`), facing `_direction` (0=down,1=up,2=left,
  3=right), and a 2-frame walk animation (`_animFrame`/`_animTimer`).
- **`TrainerNPC`** — fixed `(X, Y)`, a reference to its `AITrainer`, a `Defeated` flag, and a
  display `Label`.
- **`OverworldMap`** — owns a `Tile[25,20]` grid, the player, the NPC list, camera offset
  (`_camX`/`_camY`), and an `_encounterCooldown` counter. Exposes one-frame "did something happen"
  flags (`WildEncounterTriggered`, `TrainerEncounterTriggered`, `RegionExitTriggered`,
  `ShopEncounterTriggered`) that `GameManager.Update()` reads and resets every call to `Update()`.
- **`MapFactory`** (static, **Factory Pattern**) — three methods (`CreateRainforestMap`,
  `CreateRiverDeltaMap`, `CreateHighlandsMap`), each a hard-coded `int[20,25]` grid using
  single-letter constants (`T`=Tree, `G`=Grass, `g`=TallGrass, `W`=Water, `P`=Path, `N`=TrainerSpot,
  `X`=RegionExit, `H`=House, `S`=Shop) for readability, then constructs an `OverworldMap`.

### 3.14 `BattleAnimation` (abstract) + 8 concrete animations — [BattleAnimation.cs](../src/BattleAnimation.cs)

Every concrete animation (`HitFlashAnimation`, `DamageNumberAnimation`, `HealFlashAnimation`,
`HealNumberAnimation`, `HealParticleAnimation`, `StatusSwirlAnimation`, `CaptureOrbAnimation`,
`PotionGlowAnimation`, `TurnSkipAnimation`) extends `BattleAnimation`, which provides:
- `_frame` / `_duration` and `IsComplete => _frame >= _duration`.
- `Update()` — increments `_frame` until complete.
- abstract `Draw()`.

`GameManager` keeps a single `List<BattleAnimation> _battleAnimations`; every frame it calls
`Update()` on each, removes completed ones, and calls `Draw()` on the survivors. New animations are
pushed in by the event handlers `OnAttackHit`, `OnHealUsed`, `OnStatusApplied`, `OnTurnSkipped`.
This is **Polymorphism** again: `GameManager` never checks "what kind of animation is this" — it
just calls `.Update()`/`.Draw()` on the base type.

### 3.15 `GameManager` — [GameManager.cs](../src/GameManager.cs)

Already covered architecturally in §1.3. A few specific fields worth knowing:

- `_shopCatalog` — an array of tuples `(string Name, int Cost, Func<Item> Create)`. The `Func<Item>`
  is a **factory delegate** — buying an item calls `create()` to get a brand-new `Item` instance
  (so two purchases of "Potion" don't share state).
- `_wrappedLogCache` / `_wrappedLogCount` — a manual cache: the battle log is word-wrapped only
  when `BattleLog.Count` changes, not every frame (perf optimisation for `DrawBattleLog`).
- `_pendingEnemyTurn` / `_interTurnDelay` and `_pendingBattleEnd` / `_battleResultTimer` and
  `_pendingCaptureEnd` / `_captureEndTimer` — three independent "wait N frames, then do X" timers,
  all driven from `Update()`. This is how the battle feels turn-paced instead of instant.
- `_monsterSprites` (`Dictionary<string, Bitmap>`) — loaded once in `LoadMonsterSprites()`, looked
  up by monster name in `DrawMonsterSprite`.

### 3.16 `LogEntry` — [LogEntry.cs](../src/LogEntry.cs)

```csharp
public readonly struct LogEntry
{
    public readonly string       Text;
    public readonly LogEntryType Type;
}
```
A `struct`, not a `class` — it's a small, immutable, copy-by-value tag used purely to colour-code
battle log lines (`LogEntryType` has 14 values: `System`, `PlayerMove`, `EnemyMove`,
`StatusDamage`, `TurnSkip`, `Separator`, `TurnSeparator`, `Capture`, `Healing`, `EffectDamage`,
`EffectParalyse`, `EffectBurn`, `EffectFreeze`, `EffectPoison`).

---

## 4. OOP Principles — With Concrete Examples From This Codebase

### 4.1 Abstraction

Four abstract base classes define **what** something does without saying **how**:
- `Monster.GetBattleCry()` / `GetDescription()` — every monster has flavour text, but *what* it
  says is left to the subclass.
- `Move.Execute(Monster, Monster)` — every move "does something" to a target, but attacking,
  healing, and inflicting status are wildly different operations.
- `Item.Use(TamedMonster)` — every item is "usable", but a Potion heals while a StatusCure cures.
- `Trainer.ChooseAction(BattleState)` — every trainer "decides what to do", but a human and an AI
  decide completely differently.

`BattleManager` and `GameManager` are written entirely against these abstractions — e.g.
`BattleManager` declares `private Trainer _opponent;` (not `AITrainer`), so it works identically
whether the opponent is a regular `AITrainer` or (in principle) any future `Trainer` subclass.

### 4.2 Encapsulation

**Every field in every domain class is `private`.** Access is exclusively through:
- Read-only expression-bodied properties: `public string Name => _name;`
- Controlled-write properties with validation, e.g. `PlayerTrainer.SelectedMoveIndex`:
  ```csharp
  public int SelectedMoveIndex
  {
      get => _selectedMoveIndex;
      set
      {
          _selectedMoveIndex = value;
          // clamp to the active monster's move count
      }
  }
  ```
- Methods that mutate state under rules, e.g. `Monster.TakeDamage(int)` clamps to `>= 0` —
  external code can never set `_hp` to a negative number, because `_hp` is never exposed for
  direct assignment.

### 4.3 Inheritance

Every concrete class explicitly extends its parent with `: ParentClass`:
```
TamedMonster : Monster        WildMonster : Monster        BossMonster : Monster
AttackMove : Move              HealMove : Move              StatusMove : Move
CaptureOrb : Item               Potion : Item                StatusCure : Item
PlayerTrainer : Trainer         AITrainer : Trainer
```
`Monster` even demonstrates **constructor overloading + chaining** — there are two `protected`
constructors (single-type and dual-type), and `TamedMonster`/`WildMonster`/`BossMonster` each have
constructor overloads that call `: base(...)` with one or the other depending on whether the
monster has a `SecondaryType`.

### 4.4 Polymorphism

The clearest examples — calling a method on a **base-typed reference** and getting
subclass-specific behaviour:

- `selectedMove.Execute(attacker, defender)` in `BattleManager` — `selectedMove` is typed as
  `Move`, but the actual call resolves to `AttackMove.Execute`, `HealMove.Execute`, or
  `StatusMove.Execute` depending on the runtime type.
- `_opponent.ChooseAction(state)` — `_opponent` is typed as `Trainer`, but resolves to
  `PlayerTrainer.ChooseAction` or `AITrainer.ChooseAction`.
- `m.GetDescription()` / `m.GetBattleCry()` on a `Monster` reference — used in
  `DrawPartyScreen`/`DrawCaptureScreen` without knowing if `m` is tamed/wild/boss.
- `anim.Update()` / `anim.Draw()` on a `BattleAnimation` reference inside
  `_battleAnimations` — 8 different visual effects driven by 2 calls.
- `item.Use(active)` in `HandleItemInput` — `item` is typed `Item`, resolves to
  `Potion.Use` or `StatusCure.Use`.

### 4.5 Composition

"Owns and is responsible for the lifetime of" relationships — the contained object is created by
and dies with the owner, and is not shared:
- `Trainer` **composes** `Party` and `Inventory` — both are `new`'d inside the `Trainer`
  constructor; no other object can construct a `Party` for a trainer.
- `TamedMonster`/`WildMonster`/`BossMonster` **compose** their `Move[] _moveSet` — the moves are
  built inline in `MonsterFactory` and belong only to that monster instance.
- `GameManager` **composes** `_battleManager` — created fresh (`new BattleManager(...)`) at the
  start of every battle and discarded (set to a new instance, effectively) afterwards.
- `OverworldMap` **composes** its `Tile[,] _tiles` and `OverworldPlayer _player`.

### 4.6 Aggregation

"Holds a reference to, but does not own the lifetime of" relationships — the contained object can
exist independently and is shared/looked-up rather than created by the owner:
- `Region` **aggregates** `AITrainer`s — the same `AITrainer` instances created in
  `SetupRegions()` are also referenced by `TrainerNPC.Trainer` on the overworld map. Two different
  objects (`Region` and `OverworldMap`) hold references to the *same* `AITrainer`.
- `BattleManager` **aggregates** `_player` (a `PlayerTrainer`) and `_opponent` (a `Trainer`) — both
  are passed in via the constructor from `GameManager`/`Region`, and continue to exist after the
  `BattleManager` is discarded (the player's `PlayerTrainer` is the same object across the entire
  game).
- `TrainerNPC` **aggregates** an `AITrainer` — the NPC is a thin "where is this trainer on the
  map" wrapper; the trainer's `Party`/`Bag` data lives independently in `Region`.

---

## 5. Design Patterns

### 5.1 Singleton — `GameManager`

```csharp
private static GameManager? _instance = null;
private GameManager() { /* build everything */ }
public static GameManager GetInstance()
{
    if (_instance == null) _instance = new GameManager();
    return _instance;
}
```

**Why chosen:** there is conceptually exactly one game running, one window, one player, one game
loop. A private constructor prevents anyone from accidentally creating a second `GameManager`
(which would open a second `Window` and double the game loop).

**Advantage in this project:** `Program.Main` is a single line
(`GameManager.GetInstance().Run();`), and any future code that needs "the game" (e.g. a debug
menu) can call `GameManager.GetInstance()` from anywhere without passing references around.

### 5.2 Factory — `MonsterFactory` (and `MapFactory`)

```csharp
public static TamedMonster CreateKelabit(int level = 12) => new TamedMonster(/* ... long arg list ... */);
public static WildMonster  CreateBekantan(int level = 8) => new WildMonster(/* ... */);
public static TamedMonster Tame(WildMonster monster) => /* picks the right TamedMonster overload */;
```

**Why chosen:** constructing a monster requires ~7 arguments including a hand-built `Move[]`.
Without a factory, every call site (`SetupRegions`, `Region.GetRandomEncounter`,
`HandleItemInput`'s capture logic) would need to know and repeat that construction recipe.

**How it works:** one static method per species, default `level` parameters, and a `Tame()` method
that bridges `WildMonster → TamedMonster`. `MapFactory` is the same idea applied to
`OverworldMap` — three static methods each owning a hard-coded `int[,]` tile grid.

**Advantage:** adding a 9th monster means adding **one** new static method — nothing else in the
codebase needs to change its construction logic.

### 5.3 Strategy — `Trainer.ChooseAction(BattleState)`

```csharp
public abstract class Trainer
{
    public abstract TrainerAction ChooseAction(BattleState state);
}
```

**Why chosen:** "what move does the opponent pick?" needs to differ between a human (reads
keyboard input ahead of time) and an AI (computes best type matchup) — but `BattleManager` should
not need an `if (opponent is AITrainer)` check everywhere.

**How it works:** `BattleManager.EnemyTurn()` calls `_opponent.ChooseAction(state)` for regular
trainer battles. `AITrainer.ChooseAction` is the "concrete strategy": it loops `MoveSet`, computes
`TypeChart.GetMultiplier(move.MoveType, playerType)` (multiplied across both player types if
dual-type), and keeps the first move with the highest multiplier, storing the result in
`_selectedMoveIndex` for `BattleManager` to read afterwards.

**Advantage:** new trainer behaviours (e.g. a "healer" AI that prefers `HealMove` below 30% HP)
would only require a new `Trainer` subclass — `BattleManager` doesn't change.

> **Note:** the **boss** (Naga Belian) does *not* go through `ChooseAction` — `BattleManager`
> has a separate inline type-matchup loop for `_bossTarget`. If asked "is the boss part of the
> Strategy pattern?", the honest answer is **no** — its AI is hard-coded directly in
> `BattleManager.EnemyTurn()`, even though a dummy `AITrainer("Naga Belian")` object exists (it's
> only there to satisfy `BattleManager`'s constructor signature and is otherwise unused).

### 5.4 Observer (bonus pattern, beyond the required 3) — `BattleManager` events

```csharp
public event Action<Monster, int>? AttackHit;
public event Action<Monster, int>? HealUsed;
public event Action<Monster, StatusCondition>? StatusApplied;
public event Action<Monster, StatusCondition, int>? StatusDamage;
public event Action<Monster, StatusCondition>? TurnSkipped;
public event Action<Monster, StatusCondition>? StatusRemoved;
```

**Why chosen:** `BattleManager` has **no idea** that `BattleAnimation`s, sprites, or screen
coordinates exist (it must not draw anything, per `CLAUDE.md`). But `GameManager` needs to know
*the moment* an attack lands so it can spawn a `HitFlashAnimation` at the right pixel position.

**How it works:** `GameManager` subscribes (`+=`) to these events inside `StartWildBattle` /
`StartTrainerBattle` / `StartBossBattle`. `BattleManager.PlayerTurn`/`EnemyTurn` call
`AttackHit?.Invoke(defender, damageDealt)` etc. — `BattleManager` (the **subject**) doesn't know or
care who's listening; `GameManager` (the **observer**) reacts by pushing a new
`BattleAnimation` into `_battleAnimations`.

**Advantage:** battle *logic* (`BattleManager`) and battle *visuals* (`GameManager`/
`BattleAnimation`) are fully decoupled — you could run `BattleManager` headless (e.g. in unit
tests) with zero subscribers and it would behave identically.

> **Note:** `StatusDamage` and `StatusRemoved` are declared and fired, but `GameManager` never
> subscribes to them — they currently have **zero observers**. Burn/poison damage and status
> clearing are still logged as text (via `_battleLog`), just not animated.

---

## 6. Important Algorithms

### 6.1 Damage calculation (`AttackMove.Execute`)

```
multiplier = TypeChart.GetMultiplier(move.MoveType, target.PrimaryType)
if target.HasDualType:
    multiplier *= TypeChart.GetMultiplier(move.MoveType, target.SecondaryType)

baseDamage  = (move.Power + caster.Level * 2) * caster.AttackMultiplier   // 0.75 if caster is Burned
finalDamage = (int)(baseDamage * multiplier)
target.TakeDamage(finalDamage)
```

- Because dual-type multipliers are **multiplied together**, the maximum possible multiplier is
  `2.0 * 2.0 = 4.0` and the minimum is `0.0` (if either type is immune).
- `caster.AttackMultiplier` affects the **attacker's own output**, not the defender's incoming
  damage — a burned monster deals 25% less damage with everything, including status moves'
  underlying power (though `StatusMove` has `Power = 0` so this never matters there).
- The effectiveness phrase (`"It's super effective!"` etc.) is derived from the **combined**
  `multiplier` value via a `switch` expression matching exactly `2.0f`/`0.5f`/`0.0f` — a combined
  multiplier of e.g. `4.0f` (double-super-effective) or `0.25f` (double-resisted) does **not**
  match any case and prints no phrase, even though the damage number itself is correct.

### 6.2 Type effectiveness (`TypeChart`)

A flat `8x8 float[,]` indexed by `(int)MonsterType`. `GetMultiplier(attack, defend)` is
`O(1)` — just `_chart[(int)attack, (int)defend]`. The 8 types in enum/array order are:
`Normal, Fire, Water, Grass, Electric, Flying, Fighting, Steel`.

This single table drives **three** different systems:
1. `AttackMove.Execute` — actual damage multiplier.
2. `AITrainer.ChooseAction` / boss AI in `BattleManager.EnemyTurn` — move selection.
3. `GameManager.IsSuperEffective` / `GetTypeEffectivenessHint` — UI hints in `DrawMovePanel` and
   `DrawBattleTips` (gold border on super-effective moves, "X is super effective against Y!" text).

### 6.3 AI move selection

**Regular `AITrainer` (`AITrainer.ChooseAction`)** and **boss (`BattleManager.EnemyTurn`,
`_bossTarget` branch)** use the *same algorithm*, implemented twice:
```
bestMultiplier = -1
bestIndex = 0
for i in 0..MoveSet.Length:
    mult = TypeChart.GetMultiplier(move[i].MoveType, defenderPrimaryType)
    if defender.HasDualType:
        mult *= TypeChart.GetMultiplier(move[i].MoveType, defenderSecondaryType)
    if mult > bestMultiplier:        // strictly greater — first move wins ties
        bestMultiplier = mult
        bestIndex = i
return bestIndex
```
- This is **purely defensive type-matching** — it ignores the move's own `Power`, the AI
  monster's own type (STAB / same-type-attack-bonus is *not* modelled), and HP of either side.
- Ties always resolve to the **earliest** move in the array (because `>` not `>=`).

**Wild monsters** (`BattleManager.EnemyTurn`, `_wildTarget` branch): completely random —
`new Random().Next(moveset.Length)`. No type reasoning at all.

### 6.4 Capture probability (`CaptureOrb.AttemptCapture`)

```
catchChance = (1 - target.HpPercent) * target.CatchRate * _captureModifier
roll = Random.NextDouble()        // [0.0, 1.0)

roll <= catchChance         → Caught
roll <= catchChance * 1.5   → Escaped     (orb "almost" worked)
else                          → Failed     (orb missed entirely)
```
- Lowering the wild monster's HP is the **only** lever the player has — `CatchRate` is fixed per
  species (`0.30` Buaya → `0.70` Pelanduk) and `_captureModifier` is fixed per orb type (`1.0` for
  the standard Capture Orb).
- At full HP, `catchChance = 0`, so `Caught` is impossible and `Escaped` requires `roll <= 0`,
  which is also impossible — a full-HP monster can only ever produce `Failed`.
- `ComputeCatchChance` (the formula without the `Random` roll) is reused by the UI to show a
  Very Low/Low/Moderate/High/Very High label **before** the player commits to throwing the orb.

### 6.5 Status effects

`StatusCondition` is a **single-value enum** (`None`, `Burned`, `Frozen`, `Paralysed`,
`Poisoned`) — a monster can only be afflicted by **one** status at a time.

| Status | Inflicted by | Per-turn effect | Duration |
|---|---|---|---|
| `Burned` | `StatusMove` (always succeeds if target has no status) | `TakeDamage(maxHp * 0.10)` at the start of the afflicted monster's turn | Until... (no cure logic removes it automatically — only `StatusCure` item or being caught/fainting) |
| `Poisoned` | same | `TakeDamage(maxHp * 0.08)` | same as above |
| `Frozen` | `StatusMove` (always succeeds) | none | **Exactly 1 skipped turn**, then auto-clears to `None` |
| `Paralysed` | `StatusMove`, but only **50%** chance to actually apply (`"resisted paralysis"` otherwise) | none | **Exactly 1 skipped turn**, then auto-clears to `None` |

Order of operations each turn (`PlayerTurn`/`EnemyTurn`, identical pipeline):
1. `AddTurnSeparatorIfNew()`.
2. `ApplyStatusEffect(attacker)` — applies Burn/Poison damage *first*, regardless of what happens
   next.
3. If `Frozen` or `Paralysed` → log a skip message, clear the status to `None`, fire
   `TurnSkipped`/`StatusRemoved`, **return early** (move is never executed this turn).
4. If step 2 dealt damage, insert a visual `"──"` separator log entry.
5. Execute the chosen `Move` and log/animate the result.

---

## 7. Frequently Misunderstood Parts — Examiner Traps

1. **`GameState.Victory` is effectively unreachable.** It's declared in `Enumerations.cs`,
   `DrawVictory()` exists and is wired into the `Draw()` switch, and `HandleInput` even has a
   `case GameState.Victory:` — but **nothing in `EndBattle()` ever sets `_state = GameState.Victory`**,
   even when `_battleManager.Result == PlayerWon && _battleManager.BossTarget != null`. Defeating
   Naga Belian shows the "You defeated Naga Belian!" result overlay (from `ShowBattleResult`), then
   `EndBattle()` sends the player back to `Overworld` like any other win. If asked "what happens
   after you beat the final boss?", the honest answer is: the win message displays, then you're
   returned to the (now-cleared) overworld — the dedicated `Victory` screen is currently dead code.

2. **A monster can only have ONE status condition at a time** — `StatusCondition` is a plain enum,
   not `[Flags]`. `StatusMove.Execute()` explicitly checks
   `if (target.Status != StatusCondition.None) return "...already affected..."`. A burned monster
   **cannot also** be poisoned or paralysed.

3. **Two different "chances" govern Paralysis — don't conflate them:**
   - *Infliction chance* (in `StatusMove.Execute`): 50% chance the status **fails to apply** at
     all ("resisted paralysis").
   - *Skip chance* (in `BattleManager`): **0% chance** — once paralysed, the turn is **always**
     skipped (no "sometimes you can still move" roll like mainline Pokémon games), and the status
     clears immediately after.

4. **`AttackMultiplier` (Burned = 0.75x) reduces the *attacker's own* damage output**, not the
   damage they receive. It's easy to misread "burned monster" as "takes more damage" — that is
   **not** modelled here. Burn's only effects are (a) 10% max-HP damage per turn and (b) 25% less
   damage dealt while burned.

5. **Dual-type multipliers multiply together**, so the effective range is `0.0x` to `4.0x`, not
   capped at `2.0x`. The "It's super effective!" text only fires for a combined multiplier of
   *exactly* `2.0f` — a `4.0f` result (e.g. a Water move into a Grass/Flying-type target, since
   `2.0 * 1.0`... actually check live: a move that's `2.0` against *both* of a dual-type's types
   gives `4.0` and prints **no** effectiveness text, despite dealing quadruple damage).

6. **`MonsterFactory.Tame()` resets `Experience` to 0** regardless of the wild monster's level —
   a level-14 captured Buaya starts its `TamedMonster` life with `Experience = 0`, not "already
   most of the way to level 15".

7. **`AITrainer.ChooseAction` ties always go to the lowest-index move** because the comparison is
   strict `>`. If two moves both have a `2.0x` multiplier, the AI always picks whichever appears
   first in `MoveSet`.

8. **The boss does not use the Strategy pattern**, despite a `new AITrainer("Naga Belian", 3)`
   being constructed in `StartBossBattle()`. That `AITrainer`'s `Party` is **empty** — it exists
   purely so `BattleManager`'s constructor (`Trainer opponent`) has something to hold. The actual
   boss AI is a separate, hard-coded type-matchup loop inside `BattleManager.EnemyTurn()`'s
   `_bossTarget != null` branch. `BattleManager.EnemyMonster`'s null-coalescing chain
   (`_wildTarget ?? _bossTarget ?? _opponent.GetActiveMonster()`) ensures the empty `AITrainer`'s
   party is never actually consulted.

9. **`StatusDamage` and `StatusRemoved` events exist but have zero subscribers.** `GameManager`
   only subscribes to `AttackHit`, `HealUsed`, `StatusApplied`, `TurnSkipped`. Burn/poison damage
   and status-clear are logged as text but produce no `BattleAnimation`.

10. **`Region.Clear()` / `OverworldMap.SetCleared()` can be triggered from two different places**
    (`AdvanceRegion` when walking onto the exit tile, and `EndBattle` when the last trainer in a
    region is wiped) — this is intentional and idempotent (`_isCleared` is just a bool), not
    duplicated logic to be "cleaned up".

11. **`_turnNumber` counts *rounds*, not individual actions.** It only increments inside
    `IsBattleOver(isEndOfRound: true)`, which `GameManager` only passes as `true` *after*
    `EnemyTurn()`. A wild-battle XP reward of `50 * _turnNumber` therefore scales with how many
    full rounds (player action + enemy action) the battle took.

12. **`AddTurnSeparatorIfNew()` is only called from `PlayerTurn` and `AttemptCapture`**, never from
    `EnemyTurn` — because the enemy's action is always considered part of the *same* turn number
    as the preceding player action. The `_lastTurnSeparatorFor` field prevents duplicate `"-- Turn
    N --"` lines if `PlayerTurn` were somehow called twice in one turn.

13. **`LogEntry` is a `struct`, not a `class`.** It's an immutable value type (`readonly struct`
    with `readonly` fields) — every time it's added to `_battleLog` it's copied by value, which is
    fine because it's tiny (a string reference + an enum). If asked "why not a class here when
    everything else uses classes?", the answer is: it's a simple immutable data tag with no
    identity or behaviour — a textbook value-type use case.

14. **Sprite centring math** (`DrawMonsterSprite`, `DrawPlayerSprite`, `DrawTrainerNPC`): all
    compute `drawX = cx - bmp.Width / 2f` using the **original** (unscaled) bitmap dimensions, then
    pass a scale factor to `SplashKit.OptionScaleBmp`. This works because SplashKit's scaled draw
    scales *outward from the top-left draw position*, but the *visual centre* of the scaled image
    still lands at `(cx, cy)` only because the offset was computed from the *unscaled* size and the
    scale is applied uniformly. Getting this wrong (e.g. using `bmp.Width * scale / 2f` for the
    offset) would make sprites drift off-centre as `displaySize` changes.

15. **Three independent frame-based timers drive battle pacing**: `_interTurnDelay` (150 frames
    between player and enemy action), `_battleResultTimer` (150 frames showing the win/loss
    overlay before Enter is accepted), and `_captureEndTimer` (140 frames after a successful catch
    before switching to `CaptureScreen`). All three are plain `int` countdowns decremented in
    `Update()` — there is no use of `SplashKit` timers for these (the `"blink"` timer is the only
    `SplashKit.CreateTimer`, used purely for visual pulsing/twinkling, unrelated to game-state
    pacing).
