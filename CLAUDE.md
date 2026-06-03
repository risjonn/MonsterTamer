# BUILD & RUN COMMANDS

```bash
# Run the project (standard)
skm dotnet run

# Build without running
skm dotnet build

# Clean build artifacts
skm dotnet clean

# Restore NuGet packages
skm dotnet restore

# Run from project root (alternative if skm is globally installed)
cd MonsterTamer
skm dotnet run
```

> **Important:** Always use `skm dotnet` rather than plain `dotnet` to ensure SplashKit's native libraries are loaded. Plain `dotnet run` will compile but crash at runtime when SplashKit calls are reached.

# DEVELOPMENT RULES & CONSTRAINTS

## OOP requirements (non-negotiable)

- Minimum 4 abstract base classes: Monster, Move, Item, Trainer — all must remain abstract
- All four OOP principles must be demonstrable and traceable:
  - **Abstraction** — abstract classes with abstract methods, game logic uses base types only
  - **Encapsulation** — all fields private, access only via properties and methods
  - **Inheritance** — all child classes explicitly extend a parent via `: ParentClass`
  - **Polymorphism** — `Execute()` and `ChooseAction()` called on base type references
- 3 design patterns required for Option 4: Singleton (GameManager), Factory (MonsterFactory), Strategy (Trainer/ChooseAction)

## C# naming conventions (strictly enforced)

```
Private fields:     _camelCase      e.g. _name, _maxHp, _battleLog
Properties:         PascalCase      e.g. Name, MaxHp, IsAlive
Methods:            PascalCase      e.g. TakeDamage(), GetBattleCry()
Classes:            PascalCase      e.g. TamedMonster, BattleManager
Interfaces:         IPascalCase     e.g. IDrawable (if used)
Enums:              PascalCase      e.g. MonsterType.Fire
Local variables:    camelCase       e.g. int damage, float multiplier
Parameters:         camelCase       e.g. int amount, Monster target
Constants:          PascalCase      e.g. const int MaxSlots = 6
```

## SplashKit rules

- Always use `skm dotnet run`, never plain `dotnet run`
- All drawing must happen inside the game loop's `Draw()` call, after `SplashKit.ClearScreen()`
- Always call `SplashKit.RefreshScreen(60)` at the end of every Draw cycle
- Always call `SplashKit.ProcessEvents()` at the start of every game loop iteration
- Use `SplashKit.KeyDown()` for smooth held movement, `SplashKit.KeyTyped()` for single-press actions
- If loading PNG sprites: place files in `Resources/images/` and reference by filename without extension
- Timers: create with `SplashKit.CreateTimer("name")`, start with `SplashKit.StartTimer("name")`, read with `SplashKit.TimerTicks("name")`

## Code quality expectations

- Every abstract method must be overridden in every concrete child class
- No public fields — everything exposed must be through a property or method
- Constructors of abstract classes must be `protected`, not `public`
- No static fields except: `GameManager._instance`, `TypeChart._chart`, `MonsterFactory` methods, `MapFactory` methods
- Battle logic must not be in GameManager — delegate to BattleManager
- Drawing logic must not be in game logic classes — only GameManager and OverworldMap draw

# CURRENT TASK

### 5.6 Move animations

- [ ] Implement an animation system — a simple list of active `BattleAnimation` objects updated each frame
- [ ] **Attack move animation:**
  - Flash the target monster's position with a brief white/red overlay (3–5 frames)
  - Show a floating damage number that rises and fades out over ~40 frames
  - e.g. "-64" in red floating above the enemy sprite
- [ ] **Heal move animation:**
  - Flash the caster's position with a green overlay
  - Show a floating heal number in green e.g. "+30 HP"
  - Add small rising green particles (3–4 circles moving upward and fading)
- [ ] **Status move animation:**
  - Show swirling coloured circles around the target (colour matches status: yellow=paralysed, orange=burned, blue=frozen, purple=poisoned)
  - Display the status name as a floating text label e.g. "PARALYSED!" in the status colour
- [ ] **Capture orb animation:**
  - Draw a circle (orb) that travels from the player side toward the enemy
  - Orb expands on contact, then shows the result: "Caught!" in gold or "Escaped!" in red
- [ ] **Potion animation:**
  - Green glow around the target monster
  - Floating "+X HP" number in green
- [ ] All animations must be non-blocking — game loop continues, animations play over a fixed number of frames (use a frame counter, not Thread.Sleep)

# Architecture References

For architecture-heavy changes, consult:

- docs/ARCHITECTURE_AND_UML.md
- docs/PROJECT_SPECIFICATIONS.md

before implementation.

# Workflow

- CLAUDE.md = current task only
- docs/TODO.md = upcoming priorities
- docs/DEVELOPMENT_HISTORY.md = completed priorities
- docs/ARCHITECTURE_AND_UML.md = architecture reference
- docs/PROJECT_SPECIFICATIONS.md = assignment requirements
