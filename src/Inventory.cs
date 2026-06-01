using System.Collections.Generic;

namespace MonsterTamer
{
    public class Inventory
    {
        private List<Item> _items;
        private int        _maxSlots;

        public Inventory(int maxSlots = 20)
        {
            _items    = new List<Item>();
            _maxSlots = maxSlots;
        }

        public int Count => _items.Count;

        public Item this[int index] => _items[index];

        public bool AddItem(Item item)
        {
            if (_items.Count >= _maxSlots) return false;
            _items.Add(item);
            return true;
        }

        public void RemoveItem(Item item)
        {
            _items.Remove(item);
        }

        public List<Item> GetAllItems() => _items;
    }
}
