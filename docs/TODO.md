# TODO tasks


### 5.10 Battle screen tips and info

- [ ] Add a small "Tips" panel or info line below the move panel showing:
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
- [ ] Highlight the currently selected move's type badge more prominently when it would be super effective against the enemy


### 5.11 Item replenishment / shop system (Optional enhancement)

- [ ] Add a `ShopState` (`GameState.Shop`) accessible from the overworld (e.g. press B for bag/shop, or place a shop tile on the map near the region start)
- [ ] Shop inventory (fixed, always available):
  - Potion — restores 30 HP — free or costs game currency
  - Super Potion — restores 60 HP — higher cost
  - Capture Orb — standard catch modifier — standard cost
  - Status Cure — cures any status — standard cost
- [ ] Add a `_gold` field to `PlayerTrainer` — player earns gold by winning battles (e.g. 50–100 gold per win)
- [ ] Shop UI: simple list with item name, description, cost, and current gold shown — arrow keys to select, Enter to buy, Esc to exit
- [ ] Alternatively (simpler): place a `ShopTile` on each region map — stepping on it opens the shop
