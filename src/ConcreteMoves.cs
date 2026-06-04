namespace MonsterTamer
{
    // Child class — Inheritance from Move
    // Deals damage using type advantage multiplier — Polymorphism
    public class AttackMove : Move
    {
        public AttackMove(string name, int power, MonsterType moveType)
            : base(name, power, 10, moveType) { }

        // Overrides abstract method — Polymorphism
        public override string Execute(Monster caster, Monster target)
        {
            float multiplier = TypeChart.GetMultiplier(MoveType, target.PrimaryType);
            if (target.HasDualType)
            {
                multiplier *= TypeChart.GetMultiplier(MoveType, target.SecondaryType);
            }

            int baseDamage  = (int)((Power + (caster.Level * 2)) * caster.AttackMultiplier);
            int finalDamage = (int)(baseDamage * multiplier);
            target.TakeDamage(finalDamage);

            string effectiveness = multiplier switch
            {
                2.0f => " It's super effective!",
                0.5f => " It's not very effective.",
                0.0f => " It had no effect.",
                _    => ""
            };

            return $"{target.Name} took {finalDamage} damage!{effectiveness}";
        }

        public override string GetMoveCategory() => "Attack";
    }

    // Child class — Inheritance from Move
    // Restores HP to the caster — Polymorphism
    public class HealMove : Move
    {
        public HealMove(string name, int healAmount, MonsterType moveType)
            : base(name, healAmount, 15, moveType) { }

        // Overrides abstract method — Polymorphism
        public override string Execute(Monster caster, Monster target)
        {
            int amount = Power + (caster.Level * 2);
            caster.RestoreHp(amount);
            return $"Restored {amount} HP!";
        }

        public override string GetMoveCategory() => "Heal";
    }

    // Child class — Inheritance from Move
    // Inflicts a status condition on the target — Polymorphism
    public class StatusMove : Move
    {
        private StatusCondition _inflicts;

        public StatusMove(string name, StatusCondition inflicts, MonsterType moveType)
            : base(name, 0, 12, moveType)
        {
            _inflicts = inflicts;
        }

        // Overrides abstract method — Polymorphism
        public override string Execute(Monster caster, Monster target)
        {
            if (target.Status != StatusCondition.None)
                return $"{target.Name} is already affected by a status condition!";

            // Paralysis has a 50% infliction chance; failure means no status is applied
            if (_inflicts == StatusCondition.Paralysed && new System.Random().NextDouble() >= 0.5)
                return $"{target.Name} resisted paralysis!";

            target.Status = _inflicts;
            return $"{target.Name} is now {_inflicts}!";
        }

        public override string GetMoveCategory() => "Status";
    }
}
