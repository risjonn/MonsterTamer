# CODEBASE CHECKPOINT

Current version: **v4** (as of 2026-06-02 session — Priorities 1–5.2 complete)

## File summaries

### Program.cs

Entry point. Calls `GameManager.GetInstance().Run()`. Nothing else.

### Enumerations.cs

All 8 enums in one file: `MonsterType`, `GameState`, `TrainerAction`, `StatusCondition`, `CaptureResult`, `ItemRarity`, `BattleResult`, `LogEntryType`.

### Monster.cs

Abstract base class. All fields private. `TakeDamage()`, `RestoreHp()`, `LevelUp()`, `ApplyStatusEffect()` are concrete. `GetBattleCry()` and `GetDescription()` are abstract.

### ConcreteMonsters.cs

Three child classes in one file: `TamedMonster`, `WildMonster`, `BossMonster`. Each overrides both abstract methods. `BossMonster.GetBattleCry()` returns different text per phase.

### Move.cs

Abstract base class. `Execute()` and `GetMoveCategory()` are abstract.

### ConcreteMoves.cs

Three child classes: `AttackMove` (uses TypeChart), `HealMove` (calls RestoreHp), `StatusMove` (sets Status condition). All override Execute() and GetMoveCategory().

### Item.cs

Abstract base class. `Use()` and `GetDescription()` are abstract.

### ConcreteItems.cs

Three child classes: `CaptureOrb` (has AttemptCapture() with probability formula), `Potion` (calls RestoreHp), `StatusCure` (clears status). All override Use() and GetDescription().

### Trainer.cs

Abstract base class. Owns a `Party` and `Inventory`. `ChooseAction()` is abstract — Strategy Pattern interface.

### ConcreteTrainers.cs

`PlayerTrainer` — manages `SelectedMoveIndex` with clamping. `AITrainer` — loops through MoveSet using TypeChart to find best move. Both override `ChooseAction()`.

### TypeChart.cs

Static class. 8×8 `float[,]` lookup table. `GetMultiplier(attackType, defendType)` returns multiplier.

### BattleState.cs

Plain data class — snapshot of current battle (player monster, enemy monster, turn number, whose turn). Passed to `ChooseAction()`.

### Party.cs

Manages `List<TamedMonster>` capped at 6. Has indexer, `GetActiveMonster()`, `IsWiped()`.

### Inventory.cs

Manages `List<Item>` with configurable max slots. Has indexer, `AddItem()`, `RemoveItem()`, `GetAllItems()`.

### MonsterFactory.cs

Static Factory class. One static method per monster. Each method creates a fully-initialised monster with all moves pre-assigned. `CreateNagaBelian()` creates a `BossMonster`.

### Region.cs

Holds encounter table (`List<WildMonster>`) and trainer roster (`List<AITrainer>`). `GetRandomEncounter()` returns a fresh monster instance via MonsterFactory. `GetNextTrainer()` returns first non-wiped trainer.

### BattleManager.cs

Runs one battle. `PlayerTurn()` validates move index, applies status, calls `Execute()`. `EnemyTurn()` calls `ChooseAction()` then `Execute()`. `IsBattleOver()` checks both sides, sets `BattleResult`, awards XP on win.

### OverworldMap.cs

Contains: `TileType` enum, `Tile` class, `OverworldPlayer` class (position + animation), `TrainerNPC` class, `OverworldMap` class (tile grid + camera + collision + encounter triggers), `MapFactory` static class (3 hand-designed maps).

Movement: arrow keys and WASD. **15% chance per new tile entered** in tall grass (was 4% per frame). Trainer trigger on proximity. Region-cleared HUD badge and pulsing EXIT tile. Fade-to-black transition on EXIT. Player spawns at region-specific tile (Rainforest: 4,2; Delta/Highlands: 2,2). Camera now **centres the 800×640 map** within the 1280×720 window using negative cam offsets. `ScreenW = 1280`, `ScreenH = 720` constants defined at top of file. Fade overlay and HUD bottom bar use these constants.

### GameManager.cs

Singleton. Full game loop. Owns `OverworldMap`, `BattleManager`, `PlayerTrainer`, `List<Region>`. Contains all draw methods including per-monster geometric sprite rendering (9 unique sprites — Kelabit, Bekantan, Mawas, Rajah, Enggang, Pelanduk, Temaju, Buaya, Naga Belian).

**`ScreenW = 1280`, `ScreenH = 720` constants** defined at top — all draw calls reference these, no hardcoded 800/600 anywhere.

Screens implemented: MainMenu (redesigned for 1280×720 with title card, 4-col roster grid, bottom hints bar), Overworld (delegates to OverworldMap.Draw()), Battle (region-specific backgrounds, entrance animation, animated HP bars, result overlay), PartyScreen, Victory, GameOver.

Priority 4 visual features active: region-specific battle backgrounds, monster entrance slide-in animation (`_battleAnimFrame`), animated HP bar lerp (`_playerHpDisplay` / `_enemyHpDisplay`), `ShowBattleResult()` colour-coded overlay (2.5s, Enter to skip), animated twinkling stars + fireflies on main menu.

## Priority 1 — Critical bugs to fix

- [x] **Overworld movement** — both arrow keys and WASD work; SDL KeyCode values confirmed correct (UpKey=273 etc.)
- [x] **Battle screen enemy reference** — `BattleManager.EnemyMonster` unified property added; all draw/logic points use it
- [x] **Boss battle** — `StartBossBattle()` uses real `BossMonster` via `MonsterFactory.CreateNagaBelian()`; phase updates on each `EnemyTurn()`; `DrawNagaBelian()` sprite added

## Priority 2 — Core gameplay features not yet working

- [x] **Capture flow** — C key opens bag; CaptureOrb calls `BattleManager.AttemptCapture()`; caught monster converted via `MonsterFactory.Tame()` and added to party
- [x] **Swap monster in battle** — X key opens purple swap panel; `Party.SwapToFront()` reorders party; enemy gets a turn after swap
- [x] **Use item in battle** — bag panel shows Potions/StatusCures always, CaptureOrbs only in wild battles; items apply to active monster then enemy attacks
- [x] **Status condition visual** — `ApplyStatusEffect()` called at start of both player and enemy turns; Frozen/Paralysed skip logic implemented
- [x] **Flee/forfeit** — Esc closes sub-menus first; clean Esc calls `BattleManager.Flee()` → sets `PlayerFled` result

## Priority 3 — Overworld improvements

- [x] **Region cleared indicator** — HUD expands with green "REGION CLEARED!" badge; EXIT tile pulses gold when cleared
- [x] **Player spawn position per region** — MapFactory passes `spawnTileX/Y` per map; Rainforest spawns at (4,2), Delta/Highlands at (2,2)
- [x] **Encounter rate tuning** — changed from 4% per frame to 15% per new tile entered; uses tile-change detection (`_lastTileX/Y`)
- [x] **Region transition effect** — EXIT tile triggers fade-to-black (8 alpha/frame ≈ 0.5s); input frozen during fade; `RegionExitTriggered` fires when alpha reaches 255

## Priority 4 — Visual polish

- [x] **Battle background per region** — Rainforest uses sky-blue/green; River Delta uses misty grey-blue/olive; Highlands uses purple-dusk/rocky brown; Boss uses deep navy
- [x] **Monster entrance animation** — enemy slides in from right, player from left over 25 frames with ease-out curve (`_battleAnimFrame` counter)
- [x] **HP bar animation** — `_playerHpDisplay` / `_enemyHpDisplay` lerp toward actual `HpPercent` at 12% per frame; snaps when within 0.4%
- [x] **Battle result screen** — `ShowBattleResult()` shows colour-coded overlay (gold=win, red=loss, blue=flee) for 2.5s before `EndBattle()`; Enter skips
- [x] **Main menu animated elements** — 30 twinkling stars drift with sine-wave offsets; 18 green fireflies hover near the jungle silhouette, both driven by `TimerTicks("blink")`

## Priority 5 — UI/UX Polish & Gameplay Improvements (CURRENT FOCUS)

### 5.1 Window size

- [x] Increase window size to at least 1280×720 — window now 1280×720
- [x] After resizing, re-anchor all hardcoded x/y draw positions relative to screen width/height constants — all draw methods in GameManager updated; OverworldMap camera, tile skip, HUD, and fade overlay updated
- [x] Define `ScreenW` and `ScreenH` as constants at the top of GameManager and OverworldMap, referenced throughout all draw calls instead of hardcoded 800/600

### 5.2 Main menu redesign

- [x] Redesign main menu layout — centre-align all elements vertically and horizontally relative to 1280×720
- [x] Add a styled title card with a decorative double-border panel behind the game title (700px wide, centred)
- [x] Align monster roster list into a clean 4-column grid with consistent 200px column spacing
- [x] Add a brief one-line tagline "Explore Sarawak. Tame the Wild. Become a Guardian." below the title
- [x] Blinking "Press Enter to Start" prompt is clearly separated from the monster list and visually prominent (boxed, centred)
- [x] Key hints at the bottom bar: "Enter: Start Esc: Quit P: Party Screen"

#### 5.1/5.2 Visual bug fixes applied (2026-06-03)

- [x] Main menu title "Monster Tamer" and "Guardians of Sarawak" now centred using `SplashKit.TextWidth()` — previously used hardcoded offset estimates
- [x] "Press Enter to Start" text centred within blinking box using `TextWidth()`
- [x] Unicode star (★), middle dot (·), and em dash (—) replaced with ASCII `*` and `-` — SplashKit renders these as "?"
- [x] Overworld tile map now scales to fill the full 1280×720 window — `DrawTile` accepts `w, h` params, `Draw()` computes `scaleX = 1280/800 = 1.6`, `scaleY = 720/640 = 1.125`; player and NPC screen positions scaled accordingly; camera offset removed for small maps
- [x] Window launches fullscreen via `_window.ToggleFullscreen()` in GameManager constructor
- [x] Battle screen draw order fixed — `DrawBattleLog()` moved before info panels so log no longer occludes player info panel (both occupy x 590–1264)
- [x] Enemy info panel: orange border + "ENEMY" label; player info panel: blue border + "YOUR MONSTER" label — visually distinct

### Priority 5.1/5.2 Final Bug Fixes

Context:
Battle UI redesign has been implemented.
Remaining issues:

[x] 1. Tamed Monster info panel still overlaps visually with battle log.
[x] 2. Enemy and player info panels need stronger visual distinction.

Goal:
[x] Resolve all remaining battle UI issues before starting Priority 5.3.

### 5.3 Wild encounter prompt (do not auto-start battle)

- [x] When player walks into tall grass and a wild monster triggers, do NOT immediately start the battle
- [x] Instead show an encounter prompt overlay on the overworld screen with:
  - Monster name, level, and type badges
  - Monster sprite (reuse existing geometric sprite renderer)
  - Two options: "Fight" (Enter) and "Run" (Esc or R key)
- [x] Add a new `GameState` value: `EncounterPrompt`
- [x] Only transition to `GameState.Battle` if the player selects Fight
- [x] If player selects Run, dismiss the prompt and apply a longer encounter cooldown (~5 seconds)

### 5.4 Monster sprite improvements (battle screen)

- [x] Review each monster's geometric sprite and improve proportions — specifically:
  - Kelabit: make casque ridge more prominent, wing span wider
  - Bekantan: big nose should be the most prominent feature
  - Mawas: longer arms reaching the ground
  - Rajah: wings should dominate the sprite, more colour contrast
  - Buaya: longer body, more visible teeth row
  - Temaju: clearer scale pattern layering
  - Pelanduk: taller ears relative to body
  - Enggang: rounder bear body, more visible yellow chest patch
- [x] Add a subtle idle animation (slight vertical bob, 1-2px oscillation using timer ticks) - [x] Enemy sprite (back/front view) should be slightly larger than player sprite to indicate it is the focus

### 5.4 Monster sprite asset migration

- [x] Replace the manually drawn SplashKit geometric monster sprites with PNG sprite assets stored in Resources/Images
- [x] Use the following sprite assets:
  - bekantan.png
  - buaya.png
  - enggang.png
  - kelabit.png
  - mawas.png
  - nagabelian.png
  - pelanduk.png
  - rajah.png
  - temaju.png
- [x] All sprite assets are:
  - PNG format
  - 1024×1024 resolution
  - Transparent background
- [x] Preserve existing monster-to-sprite mapping
- [x] Continue displaying monster sprites in:
  - Battle screen
  - Wild EncounterPrompt
  - Boss EncounterPrompt
  - Any existing monster preview UI
- [x] Preserve the idle animation introduced in Priority 5.4:
  - subtle vertical bob (1–2px oscillation)
- [x] Enemy sprites should remain slightly larger than player sprites
- [x] Implement efficient sprite loading:
  - load once
  - cache/reuse
  - do not reload every frame
- [x] Remove or retire obsolete geometric sprite rendering code if no longer required
- [x] Preserve all existing gameplay behaviour and UI layouts

### 5.5 UI Layout & Readability Pass

- [x] Fix move box layout — all text must be clipped or wrapped inside the move box bounds:
  - Move name: truncate with "..." if too long for box width
  - PWR value: right-align inside the box, not outside
  - Type badge: must sit inside the bottom of the move box
  - Category label: sits below move name, inside box

- [x] Fix battle log text overflow — wrap long sentences across multiple lines inside the log box:
  - Implement a simple word-wrap function
  - Split strings by spaces and build lines that fit within log box width
  - Show the most recent 4–5 wrapped lines
  - Oldest wrapped lines scroll off the top

- [x] Fix info panel layout — ensure HP bar, type badges, name, and level label remain within panel bounds:
  - Must work correctly for all monster name lengths
  - Must work correctly for all type badge combinations

- [x] Add a thin separator line between each move box

- [x] Ensure all battle UI panels use consistent padding:
  - 10px inner margin minimum
  - Consistent spacing between UI elements

- [x] Fix battle command hint layout:
  - "Arrows/Enter: Use Move"
  - "C: Bag"
  - "X: Swap"
  - "Esc: Flee"
  - All command hints must remain fully visible inside their panel
  - No text overflow outside the move selection box
  - Maintain readable spacing between hints

- [x] Fix type badge sizing throughout battle-related UI:
  - Type names must always fit inside their coloured badge
  - Long type names (e.g. "Electric") must not overflow badge boundaries
  - Dynamically size badge width based on text width and padding

- [x] Fix Wild EncounterPrompt layout:
  - Monster sprite must not overlap monster information
  - Monster name, level, and type badges must remain fully readable
  - Reposition layout elements to accommodate PNG sprite artwork
  - Maintain visual balance within the encounter overlay

- [x] Fix Boss EncounterPrompt layout:
  - Party monster sprites must not overlap monster information
  - Monster name, level, and type badges must remain fully readable
  - Support trainers with multiple monsters
  - Maintain consistent spacing between party entries

- [x] Review all battle and encounter overlays after PNG sprite migration:
  - Ensure text, badges, sprites, and panels do not overlap
  - Ensure all UI remains readable at 1280×720
  - Preserve existing functionality and animations

### 5.6 Status condition logic fix

- [x] **Paralysed**:
  - 50% chance to skip the turn entirely
  - Show "X is paralysed and cannot move!"
  - Do not execute the selected move if the turn is skipped
- [x] **Frozen**:
  - Always skip the next turn
  - Show "X is frozen solid!"
  - Clear the status after the skipped turn
- [x] **Burned**:
  - Deal 10% max HP damage at the start of the turn
  - Reduce attack power by 25%
  - Show "X is hurt by its burn!"
- [x] **Poisoned**:
  - Deal 8% max HP damage at the start of the turn
  - Show "X is hurt by poison!"
- [x] Apply status effects before the monster acts
- [x] Ensure skipped turns do not execute moves
- [x] Improve battle log readability:
  - Add visual separation between status events and move execution
  - Clearly distinguish skipped turns from normal actions
- [x] Verify status effects work correctly for both player and enemy monsters
- [x] Prepare hooks/events for future battle animations (Priority 5.7):
  - StatusApplied
  - StatusDamage
  - TurnSkipped
  - StatusRemoved

### 5.7a Battle animation framework & attack animations

- [x] New `BattleAnimation` abstract class in `src/BattleAnimation.cs` — `_frame`, `_duration`, `IsComplete`, concrete `Update()`, abstract `Draw()`
- [x] `HitFlashAnimation` — 5-frame white-then-red semi-transparent circle over target sprite centre; fades with each frame
- [x] `DamageNumberAnimation` — 40-frame floating "-N" damage number in red; rises 55px and shrinks/fades as `t → 1`
- [x] `BattleManager.AttackHit` event — fires `(Monster target, int damage)` using HP delta around `Execute()`, never parsing result strings
- [x] `GameManager` subscribes to `AttackHit` in `StartWildBattle`, `StartTrainerBattle`, `StartBossBattle`
- [x] `OnAttackHit()` resolves target side (player vs enemy) by reference equality against `EnemyMonster`; uses sprite centre coordinates (enemy: 845,168; player: 272,312)
- [x] Animation list pruned in `Update()` each frame (reverse-iterate, remove `IsComplete`); drawn in `DrawBattle()` after sprites, before UI panels
- [x] `ResetBattleAnimState()` clears animation list on battle start
- [x] All animations are non-blocking — no `Thread.Sleep`, only frame counters
- [x] Reuse hooks ready for 5.7b: `StatusApplied`, `StatusDamage`, `TurnSkipped`, `StatusRemoved` events already wired in `BattleManager`

### 5.7b Heal, status & turn-skip animations

- [x] Heal move animation:
  - Green flash on caster
  - Floating green heal number
  - Example: "+30 HP"
  - 3–4 rising green particles that fade out
- [x] Status move animation:
  - Swirling coloured circles around target
  - Status colours:
    - Yellow = Paralysed
    - Orange = Burned
    - Blue = Frozen
    - Purple = Poisoned
  - Floating status label
  - Example: "PARALYSED!"
- [x]Turn skip animation:
  - Trigger when Paralysed or Frozen causes a skipped turn
  - Brief status-coloured flash
  - Floating "SKIPPED!" text
- [x] Reuse animation framework from Priority 5.7a
- [x] All animations must:
  - Be non-blocking
  - Use frame counters only
  - Work for both player and enemy monsters

### 5.7c Capture & item animations

- [x] Capture orb animation:
  - Orb travels from player side toward enemy
  - Orb expands on contact
  - Show result:
    - "Caught!" in gold
    - "Escaped!" in red
- [x] Potion animation:
  - Green glow around target monster
  - Floating "+X HP" text
- [x] Reuse animation framework from Priority 5.7a
- [x] All animations must:
  - Be non-blocking
  - Use frame counters only
  - Work correctly with existing battle flow

### 5.8 Capture result screen

- [x] New `GameState.CaptureScreen` added to `Enumerations.cs`
- [x] `CaptureOrb.ComputeCatchChance(WildMonster)` added to `ConcreteItems.cs` — DRY refactor of internal formula; reused by `AttemptCapture()`
- [x] On successful capture: WildMonster snapshot (name, level, types, HP, MaxHP) stored before taming; catch-chance label computed (Very Low / Low / Moderate / High / Very High at <0.10 / <0.20 / <0.35 / <0.50 / >=0.50)
- [x] `_pendingCaptureEnd` timer now transitions to `GameState.CaptureScreen` instead of calling `EndBattle()` directly
- [x] `DrawCaptureScreen()` — dark-blue full-screen; gold "CAPTURED!" title; monster sprite (non-player scale, with idle bob); gold-bordered info card showing name, level, type badges, HP at capture, catch-chance label, and party message; "Press Enter to continue" at bottom
- [x] Party message colour: cyan when added, amber when released (party full)
- [x] Enter and Esc on `CaptureScreen` both call `EndBattle()` → returns to overworld
- [x] Failed captures unchanged — orb animation plays, battle resumes normally

### 5.9 Battle log improvements

- [x] Colour-code battle log entries:
  - Player's move results: bright white or light blue
  - Enemy's move results: orange or light red
  - Status condition messages: colour matching the status (yellow, orange, blue, purple)
  - System messages (battle start, capture result, turn skips): grey or italic style
  - Damage numbers in log: red for damage taken, green for healing
- [x] Add a small icon prefix per log entry type (e.g. ">")
- [x] Show turn number at the start of each new turn as a separator in the log e.g. "-- Turn 3 --"
- [x] Most recent entry should be at the bottom and most visually prominent (full opacity), older entries fade

### 5.10 Battle screen tips and info

- [x] Add a small "Tips" panel or info line below the move panel showing:
  - Type effectiveness hint:
    - Example: "Flying is super effective against Grass!" based on current enemy type vs selected move type
  - Capture hint (only if player has a Capture Orb):
    - Show estimated capture chance based on current battle state
    - Example:
      - "Capture Chance: Very Low"
      - "Capture Chance: Low"
      - "Capture Chance: Moderate"
      - "Capture Chance: High"
      - "Capture Chance: Very High"
  - Capture chance should update dynamically as enemy HP changes
- [x] Highlight the currently selected move's type badge more prominently when it would be super effective against the enemy

### 5.11 Item replenishment / shop system (Optional enhancement)

- [x] Add a `ShopTile` in overworld maps (region-based placement near start or key locations)
  - Stepping on a ShopTile opens shop UI
  - Exiting shop returns to overworld state
- [x] Shop inventory (fixed, always available):
  - Potion — restores 30 HP — free or costs game currency
  - Super Potion — restores 60 HP — higher cost
  - Capture Orb — standard catch modifier — standard cost
  - Status Cure — cures any status — standard cost
- [x] Add a `_gold` field to `PlayerTrainer` — player earns gold by winning battles (e.g. 50–100 gold per win)
- [x] Shop UI: simple list with item name, description, cost, and current gold shown — arrow keys to select, Enter to buy, Esc to exit

### 5.12 Audio Improvements

- [x] Add background music (BGM) system using existing audio files in:
      `resources/audio/`
- [ ] Use the following tracks:
  - `overworld.ogg` → plays during overworld gameplay
  - `battle.ogg` → plays during battle state
- [x] Audio behavior requirements:
  - Overworld BGM loops continuously while in overworld state
  - Battle BGM loops continuously while in battle state
  - Smooth transition between tracks when switching states
  - Prevent overlapping audio (only one BGM playing at any time)
  - Stop current track before starting new track on state change

### 5.13 Trainer & Region Boss Sprite Upgrade

- [x] Replace overworld manually drawn visuals with PNG sprite rendering from:
  `resources/images/`

- [x] Trainer update:
  - Rename "Ranger Siti" → "Ranger Lili"
  - Use sprite-based rendering for trainer:
    - trainer.png (default/down)
    - trainer_back.png (up)
    - trainer_left.png
    - trainer_right.png
  - Movement direction rule:
    - Trainer sprite MUST update based on last movement direction
    - Moving up → use trainer_back.png
    - Moving down → use trainer.png
    - Moving left → use trainer_left.png
    - Moving right → use trainer_right.png
    - Sprite must persist direction when idle (no movement)

- [x] Region boss sprites:
  - Ranger Lili → ranger_lili.png
  - Ranger Azman → ranger_azman.png
  - Champion Dayak → champion_dayak.png

- [x] Requirements:
  - Sprites must follow existing overworld positioning/grid logic
  - Maintain current movement and collision system (visual replacement only)
  - Ensure correct sprite selection is fully driven by movement direction state

### 5.14 Battle Bag UI Improvements
- [x] Standardize bag item slot dimensions and spacing to match the move panel layout
- [x] Ensure bag control hints remain fully visible and never overlap item slots
- [x] Add inventory scrolling support:
  - Display up to 4 item slots at a time
  - Allow selection of items beyond the visible range
  - Automatically scroll the visible item window as selection moves
- [x] Add scroll indicators:
  - Show an upward indicator when items exist above the current visible range
  - Show a downward indicator when items exist below the current visible range
- Requirements:
  - Preserve existing bag functionality and item usage logic
  - Preserve existing battle panel layout
  - Ensure the selected item is always visible on screen
  - Support inventories larger than 4 items

## Priority 6 — Report & Final Submission (do in main chat, not Claude Code)

### 6.1 Class diagram updates (address lecturer feedback)

- [ ] Add abstract methods to abstract class boxes in italics:
  - Monster: _GetBattleCry()_, _GetDescription()_
  - Move: _Execute()_, _GetMoveCategory()_
  - Item: _Use()_, _GetDescription()_
  - Trainer: _ChooseAction()_
- [ ] Add overriding methods to each child class box (same method names, not italicised)
- [ ] Add fields inside class boxes that justify relationship arrows:
  - PlayerTrainer box: show `_party : Party`, `_bag : Inventory`
  - Party box: show `_monsters : List<TamedMonster>`
  - GameManager box: show `_instance : GameManager` (underlined, static), `_player : PlayerTrainer`, `_currentRegion : Region`
  - Inventory box: show `_items : List<Item>`
- [ ] Add UML note boxes for each design pattern near relevant classes:
  - Near GameManager: "Singleton Pattern — only one instance via GetInstance()"
  - Near MonsterFactory: "Factory Pattern — centralises monster creation"
  - Near Trainer/PlayerTrainer/AITrainer: "Strategy Pattern — ChooseAction() is the strategy interface"
- [ ] Redraw Figure 4 (Updated Class Diagram) to reflect all the above

### 6.2 Sequence diagram (no changes needed — already finalised)

### 6.3 Additional features section (already rewritten — verify it matches final code)

### 6.4 Sample output

- [ ] Take new screenshots showing: main menu, overworld movement, encounter prompt, battle screen with animations, capture result screen, party screen, shop
- [ ] Consider a short screen recording (GIF or MP4) showing a full gameplay loop

### 6.5 Required Roles tables — add missing classes

- [ ] Add table for `BattleState` class
- [ ] Add table for `BattleResult` enum
- [ ] Add table for `MonsterFactory` (static class, Factory Pattern)
- [ ] Add table for `OverworldMap` class
- [ ] Add table for `OverworldPlayer` class
- [ ] Add table for `TrainerNPC` class
- [ ] Add table for `TypeChart` (static class)
- [ ] Add table for `MapFactory` (static class)

### 6.6 Final playthrough checklist

- [ ] MainMenu → Enter → Overworld loads correctly
- [ ] WASD/Arrow movement works, camera follows player
- [ ] Walking into tall grass triggers encounter prompt (not instant battle)
- [ ] Selecting Fight starts battle, selecting Run dismisses prompt
- [ ] Battle: all 4 move types work correctly (Attack with type effectiveness, Heal, Status with correct skip logic)
- [ ] Capture: orb throw animation plays, result screen shows on catch
- [ ] Win battle: XP awarded, party screen shows updated level if levelled up
- [ ] Lose battle: game over screen
- [ ] Flee: returns to overworld
- [ ] Shop: can buy potions and orbs with earned gold
- [ ] All 3 regions accessible, trainer battles work, region clears after all trainers defeated
- [ ] EXIT tile transitions to next region with fade
- [ ] Final boss (Naga Belian) triggers after all regions cleared, 3 phases work
- [ ] Victory screen after defeating Naga Belian
