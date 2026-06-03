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


### 5.6 Status condition logic fix

- [ ] **Paralysed**:
  - 50% chance to skip the turn entirely
  - Show "X is paralysed and cannot move!"
  - Do not execute the selected move if the turn is skipped
- [ ] **Frozen**:
  - Always skip the next turn
  - Show "X is frozen solid!"
  - Clear the status after the skipped turn
- [ ] **Burned**:
  - Deal 10% max HP damage at the start of the turn
  - Reduce attack power by 25%
  - Show "X is hurt by its burn!"
- [ ] **Poisoned**:
  - Deal 8% max HP damage at the start of the turn
  - Show "X is hurt by poison!"
- [ ] Apply status effects before the monster acts
- [ ] Ensure skipped turns do not execute moves
- [ ] Improve battle log readability:
  - Add visual separation between status events and move execution
  - Clearly distinguish skipped turns from normal actions
- [ ] Verify status effects work correctly for both player and enemy monsters
- [ ] Prepare hooks/events for future battle animations (Priority 5.7):
  - StatusApplied
  - StatusDamage
  - TurnSkipped
  - StatusRemoved


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
