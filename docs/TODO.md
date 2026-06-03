# TODO tasks


### 5.7 Status condition logic fix

- [ ] **Paralysed:** 50% chance to skip the turn entirely — if skipped, show "X is paralysed and cannot move!" in the battle log and skip calling Execute() for that turn
- [ ] **Frozen:** Always skip next turn, then thaw — show "X is frozen solid!" and skip turn, then clear the status
- [ ] **Burned:** Deals 10% max HP damage at the start of the turn AND reduces attack power by 25% — show "X is hurt by its burn!" in log
- [ ] **Poisoned:** Deals 8% max HP damage at the start of the turn — show "X is hurt by poison!"
- [ ] Status is applied BEFORE the affected monster acts — if paralysed stops the turn, the monster does NOT use a move that turn
- [ ] Show a clear animated indicator when a turn is skipped due to status (see 5.6)
- [ ] In the battle log, add a visual separator or colour distinction when a turn is skipped vs when a move is used

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
