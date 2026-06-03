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

### 5.5 UI Layout & Readability Pass

- [ ] Fix move box layout — all text must be clipped or wrapped inside the move box bounds:
  - Move name: truncate with "..." if too long for box width
  - PWR value: right-align inside the box, not outside
  - Type badge: must sit inside the bottom of the move box
  - Category label: sits below move name, inside box

- [ ] Fix battle log text overflow — wrap long sentences across multiple lines inside the log box:
  - Implement a simple word-wrap function
  - Split strings by spaces and build lines that fit within log box width
  - Show the most recent 4–5 wrapped lines
  - Oldest wrapped lines scroll off the top

- [ ] Fix info panel layout — ensure HP bar, type badges, name, and level label remain within panel bounds:
  - Must work correctly for all monster name lengths
  - Must work correctly for all type badge combinations

- [ ] Add a thin separator line between each move box

- [ ] Ensure all battle UI panels use consistent padding:
  - 10px inner margin minimum
  - Consistent spacing between UI elements

- [ ] Fix battle command hint layout:
  - "Arrows/Enter: Use Move"
  - "C: Bag"
  - "X: Swap"
  - "Esc: Flee"
  - All command hints must remain fully visible inside their panel
  - No text overflow outside the move selection box
  - Maintain readable spacing between hints

- [ ] Fix type badge sizing throughout battle-related UI:
  - Type names must always fit inside their coloured badge
  - Long type names (e.g. "Electric") must not overflow badge boundaries
  - Dynamically size badge width based on text width and padding

- [ ] Fix Wild EncounterPrompt layout:
  - Monster sprite must not overlap monster information
  - Monster name, level, and type badges must remain fully readable
  - Reposition layout elements to accommodate PNG sprite artwork
  - Maintain visual balance within the encounter overlay

- [ ] Fix Boss EncounterPrompt layout:
  - Party monster sprites must not overlap monster information
  - Monster name, level, and type badges must remain fully readable
  - Support trainers with multiple monsters
  - Maintain consistent spacing between party entries

- [ ] Review all battle and encounter overlays after PNG sprite migration:
  - Ensure text, badges, sprites, and panels do not overlap
  - Ensure all UI remains readable at 1280×720
  - Preserve existing functionality and animations

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
