# TODO tasks

### 5.14 Battle Bag UI Improvements
- [ ] Standardize bag item slot dimensions and spacing to match the move panel layout
- [ ] Ensure bag control hints remain fully visible and never overlap item slots
- [ ] Add inventory scrolling support:
  - Display up to 4 item slots at a time
  - Allow selection of items beyond the visible range
  - Automatically scroll the visible item window as selection moves
- [ ] Add scroll indicators:
  - Show an upward indicator when items exist above the current visible range
  - Show a downward indicator when items exist below the current visible range
- Requirements:
  - Preserve existing bag functionality and item usage logic
  - Preserve existing battle panel layout
  - Ensure the selected item is always visible on screen
  - Support inventories larger than 4 items