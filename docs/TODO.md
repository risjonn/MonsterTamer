# TODO tasks


### 5.7 Move animations

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


### 5.8 Battle log improvements

- [ ] Colour-code battle log entries:
  - Player's move results: bright white or light blue
  - Enemy's move results: orange or light red
  - Status condition messages: colour matching the status (yellow, orange, blue, purple)
  - System messages (battle start, capture result, turn skips): grey or italic style
  - Damage numbers in log: red for damage taken, green for healing
- [ ] Add a small icon prefix per log entry type (e.g. "▶" for player, "◀" for enemy, "★" for status, "♥" for heal)
- [ ] Show turn number at the start of each new turn as a separator in the log e.g. "── Turn 3 ──"
- [ ] Most recent entry should be at the bottom and most visually prominent (full opacity), older entries fade

### 5.9 Battle screen tips and info

- [ ] Add a small "Tips" panel or info line below the move panel showing:
  - Type effectiveness hint: e.g. "Flying is super effective against Grass!" based on current enemy type vs selected move type
  - Capture hint: show current estimated capture % based on enemy HP (e.g. "Catch chance: ~34%") — only show this if player has a Capture Orb in bag
  - Status reminder: if enemy has a status, show "Enemy: Paralysed — 50% chance to skip"
- [ ] Highlight the currently selected move's type badge more prominently when it would be super effective against the enemy

### 5.10 Capture result screen

- [ ] When a monster is successfully caught (`CaptureResult.Caught`), show a dedicated result screen (similar to victory/defeat) before returning to overworld:
  - Display the caught monster's name, level, and type badges
  - Show the monster's geometric sprite
  - Show its catch stats: HP remaining, catch rate, result
  - Message: "X was added to your party!" or "X was released — party full!" if party is at 6
  - Press Enter to continue
- [ ] If the monster escaped or the orb failed, show a brief message overlay (not a full screen) and return to battle

### 5.11 Item replenishment / shop system

- [ ] Add a `ShopState` (`GameState.Shop`) accessible from the overworld (e.g. press B for bag/shop, or place a shop tile on the map near the region start)
- [ ] Shop inventory (fixed, always available):
  - Potion — restores 30 HP — free or costs game currency
  - Super Potion — restores 60 HP — higher cost
  - Capture Orb — standard catch modifier — standard cost
  - Status Cure — cures any status — standard cost
- [ ] Add a `_gold` field to `PlayerTrainer` — player earns gold by winning battles (e.g. 50–100 gold per win)
- [ ] Shop UI: simple list with item name, description, cost, and current gold shown — arrow keys to select, Enter to buy, Esc to exit
- [ ] Alternatively (simpler): place a `ShopTile` on each region map — stepping on it opens the shop
