namespace MonsterTamer
{
    // Abstract base class — Abstraction + Polymorphism
    public abstract class Item
    {
        private string     _name;
        private string     _description;
        private ItemRarity _rarity;

        protected Item(string name, string description, ItemRarity rarity)
        {
            _name        = name;
            _description = description;
            _rarity      = rarity;
        }

        // Properties
        public string     Name        => _name;
        public ItemRarity Rarity      => _rarity;

        // Abstract methods — Polymorphism
        // Each item subclass uses differently
        public abstract void   Use(TamedMonster target);
        public abstract string GetDescription();
    }
}
