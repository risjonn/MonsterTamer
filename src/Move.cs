namespace MonsterTamer
{
    // Abstract base class — evolved directly from Spell in Wizard Simulator
    // Abstraction + Polymorphism
    public abstract class Move
    {
        private string      _name;
        private int         _power;
        private int         _manaCost;
        private MonsterType _moveType;

        protected Move(string name, int power, int manaCost, MonsterType moveType)
        {
            _name     = name;
            _power    = power;
            _manaCost = manaCost;
            _moveType = moveType;
        }

        // Properties
        public string      Name     => _name;
        public int         Power    => _power;
        public int         ManaCost => _manaCost;
        public MonsterType MoveType => _moveType;

        // Abstract methods — Polymorphism
        // Called identically on any Move, behaves differently per subclass
        public abstract string Execute(Monster caster, Monster target);
        public abstract string GetMoveCategory();
    }
}
