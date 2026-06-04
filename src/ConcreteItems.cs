using System;

namespace MonsterTamer
{
    // Child class — Inheritance from Item
    // Attempts to catch a wild monster — Polymorphism
    public class CaptureOrb : Item
    {
        private float _captureModifier;
        private static Random _rng = new Random();

        public CaptureOrb(string name, float captureModifier, ItemRarity rarity)
            : base(name, $"An orb used to catch wild monsters.", rarity)
        {
            _captureModifier = captureModifier;
        }

        // Overrides abstract method — Polymorphism
        public override void Use(TamedMonster target)
        {
            // CaptureOrb.Use() is called on a TamedMonster placeholder
            // Actual catch logic is handled by BattleManager using AttemptCapture()
        }

        public float ComputeCatchChance(WildMonster target)
            => (1.0f - target.HpPercent) * target.CatchRate * _captureModifier;

        public CaptureResult AttemptCapture(WildMonster target)
        {
            // Probability increases as target HP decreases
            float catchChance = ComputeCatchChance(target);
            float roll        = (float)_rng.NextDouble();

            if (roll <= catchChance)        return CaptureResult.Caught;
            if (roll <= catchChance * 1.5f) return CaptureResult.Escaped;
            return CaptureResult.Failed;
        }

        // Overrides abstract method — Polymorphism
        public override string GetDescription()
        {
            return $"{Name} - Capture modifier: {_captureModifier:F1}x";
        }
    }

    // Child class — Inheritance from Item
    // Restores HP to a monster — Polymorphism
    public class Potion : Item
    {
        private int _healAmount;

        public Potion(string name, int healAmount, ItemRarity rarity)
            : base(name, $"Restores {healAmount} HP.", rarity)
        {
            _healAmount = healAmount;
        }

        // Overrides abstract method — Polymorphism
        public override void Use(TamedMonster target)
        {
            target.RestoreHp(_healAmount);
        }

        public override string GetDescription()
        {
            return $"{Name} - Restores {_healAmount} HP";
        }
    }

    // Child class — Inheritance from Item
    // Removes a status condition — Polymorphism
    public class StatusCure : Item
    {
        private StatusCondition _cures;

        public StatusCure(string name, StatusCondition cures, ItemRarity rarity)
            : base(name, $"Cures {cures} status condition.", rarity)
        {
            _cures = cures;
        }

        // Overrides abstract method — Polymorphism
        public override void Use(TamedMonster target)
        {
            if (target.Status == _cures)
                target.Status = StatusCondition.None;
        }

        public override string GetDescription()
        {
            return $"{Name} - Cures {_cures}";
        }
    }
}
