namespace MonsterTamer
{
    // Child class — Inheritance from Monster
    // Player-owned monster that gains experience
    public class TamedMonster : Monster
    {
        private string _battleCry;
        private string _description;
        private int    _experience;
        private Move[] _moveSet;

        public TamedMonster(string name, int maxHp, int level,
                            MonsterType primary, MonsterType secondary,
                            string battleCry, string description,
                            Move[] moveSet)
            : base(name, maxHp, level, primary, secondary)
        {
            _battleCry   = battleCry;
            _description = description;
            _experience  = 0;
            _moveSet     = moveSet;
        }

        public TamedMonster(string name, int maxHp, int level,
                            MonsterType primary,
                            string battleCry, string description,
                            Move[] moveSet)
            : base(name, maxHp, level, primary)
        {
            _battleCry   = battleCry;
            _description = description;
            _experience  = 0;
            _moveSet     = moveSet;
        }

        public int    Experience => _experience;
        public Move[] MoveSet    => _moveSet;

        public void GainExperience(int amount)
        {
            _experience += amount;
            int threshold = Level * 100;
            if (_experience >= threshold)
            {
                _experience -= threshold;
                LevelUp();
            }
        }

        // Overrides abstract methods — Polymorphism
        public override string GetBattleCry()   => _battleCry;
        public override string GetDescription() => _description;
    }

    // Child class — Inheritance from Monster
    // Wild encounter monster, catchable by the player
    public class WildMonster : Monster
    {
        private string _battleCry;
        private string _description;
        private float  _catchRate;
        private Move[] _moveSet;

        public WildMonster(string name, int maxHp, int level,
                           MonsterType primary,
                           string battleCry, string description,
                           Move[] moveSet, float catchRate = 0.5f)
            : base(name, maxHp, level, primary)
        {
            _battleCry   = battleCry;
            _description = description;
            _catchRate   = catchRate;
            _moveSet     = moveSet;
        }

        public WildMonster(string name, int maxHp, int level,
                           MonsterType primary, MonsterType secondary,
                           string battleCry, string description,
                           Move[] moveSet, float catchRate = 0.5f)
            : base(name, maxHp, level, primary, secondary)
        {
            _battleCry   = battleCry;
            _description = description;
            _catchRate   = catchRate;
            _moveSet     = moveSet;
        }

        public float  CatchRate => _catchRate;
        public Move[] MoveSet   => _moveSet;

        // Overrides abstract methods — Polymorphism
        public override string GetBattleCry()   => _battleCry;
        public override string GetDescription() => _description;
    }

    // Child class — Inheritance from Monster
    // Final boss with multiple phases, uncatchable
    public class BossMonster : Monster
    {
        private string _battleCry;
        private int    _phase;
        private int    _totalPhases;
        private Move[] _moveSet;

        public BossMonster(string name, int maxHp, int level,
                           MonsterType primary, MonsterType secondary,
                           string battleCry, Move[] moveSet, int totalPhases = 3)
            : base(name, maxHp, level, primary, secondary)
        {
            _battleCry   = battleCry;
            _phase       = 1;
            _totalPhases = totalPhases;
            _moveSet     = moveSet;
        }

        public int    Phase       => _phase;
        public int    TotalPhases => _totalPhases;
        public Move[] MoveSet     => _moveSet;

        public void UpdatePhase()
        {
            float hpPercent = HpPercent;
            if (_totalPhases == 3)
            {
                if      (hpPercent <= 0.33f) _phase = 3;
                else if (hpPercent <= 0.66f) _phase = 2;
                else                         _phase = 1;
            }
        }

        // Overrides abstract methods — Polymorphism
        public override string GetBattleCry()
        {
            return _phase switch
            {
                1 => _battleCry,
                2 => "NAGA BELIAN AWAKENS!",
                3 => "THE SPIRIT UNLEASHED!",
                _ => _battleCry
            };
        }

        public override string GetDescription()
        {
            return $"Naga Belian — Phase {_phase} of {_totalPhases}  |  Water / Dragon / Psychic";
        }
    }
}
