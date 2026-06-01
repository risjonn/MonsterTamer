namespace MonsterTamer
{
    // Abstract base class — Abstraction + Encapsulation
    public abstract class Monster
    {
        // Private fields — Encapsulation
        private string _name;
        private int _hp;
        private int _maxHp;
        private int _level;
        private MonsterType _primaryType;
        private MonsterType _secondaryType;
        private bool _hasDualType;
        private StatusCondition _statusCondition;

        // Constructor — single type
        protected Monster(string name, int maxHp, int level, MonsterType primaryType)
        {
            _name             = name;
            _maxHp            = maxHp;
            _hp               = maxHp;
            _level            = level;
            _primaryType      = primaryType;
            _hasDualType      = false;
            _statusCondition  = StatusCondition.None;
        }

        // Constructor — dual type
        protected Monster(string name, int maxHp, int level,
                          MonsterType primaryType, MonsterType secondaryType)
        {
            _name             = name;
            _maxHp            = maxHp;
            _hp               = maxHp;
            _level            = level;
            _primaryType      = primaryType;
            _secondaryType    = secondaryType;
            _hasDualType      = true;
            _statusCondition  = StatusCondition.None;
        }

        // Properties — controlled access
        public string          Name            => _name;
        public int             Hp              => _hp;
        public int             MaxHp           => _maxHp;
        public int             Level           => _level;
        public MonsterType     PrimaryType     => _primaryType;
        public MonsterType     SecondaryType   => _secondaryType;
        public bool            HasDualType     => _hasDualType;
        public bool            IsAlive         => _hp > 0;
        public float           HpPercent       => (float)_hp / _maxHp;
        public StatusCondition Status
        {
            get => _statusCondition;
            set => _statusCondition = value;
        }

        // Concrete methods — shared across all monsters
        public void TakeDamage(int amount)
        {
            _hp -= amount;
            if (_hp < 0) _hp = 0;
        }

        public void RestoreHp(int amount)
        {
            _hp += amount;
            if (_hp > _maxHp) _hp = _maxHp;
        }

        public void LevelUp()
        {
            _level++;
            int hpIncrease = 10;
            _maxHp += hpIncrease;
            _hp    += hpIncrease;
        }

        public void ApplyStatusEffect()
        {
            switch (_statusCondition)
            {
                case StatusCondition.Burned:
                    TakeDamage((int)(_maxHp * 0.1f));
                    break;
                case StatusCondition.Poisoned:
                    TakeDamage((int)(_maxHp * 0.08f));
                    break;
                case StatusCondition.Frozen:
                case StatusCondition.Paralysed:
                case StatusCondition.None:
                    break;
            }
        }

        // Abstract methods — Polymorphism
        // Each subclass overrides these differently
        public abstract string GetBattleCry();
        public abstract string GetDescription();
    }
}
