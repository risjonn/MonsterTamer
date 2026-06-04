# TODO tasks


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
