namespace MonsterTamer
{
    // Child class — Inheritance from Trainer
    // Strategy Pattern — concrete strategy: reads keyboard input
    public class PlayerTrainer : Trainer
    {
        private int _selectedMoveIndex;
        private int _selectedPartyIndex;

        public PlayerTrainer(string name) : base(name)
        {
            _selectedMoveIndex  = 0;
            _selectedPartyIndex = 0;
        }

        public int SelectedMoveIndex
        {
            get => _selectedMoveIndex;
            set
            {
                _selectedMoveIndex = value;
                TamedMonster? active = GetActiveMonster();
                if (active != null)
                {
                    if (_selectedMoveIndex < 0)
                        _selectedMoveIndex = 0;
                    if (_selectedMoveIndex >= active.MoveSet.Length)
                        _selectedMoveIndex = active.MoveSet.Length - 1;
                }
            }
        }

        // Overrides abstract method — Strategy Pattern / Polymorphism
        // Player strategy: action is set externally via input handling
        public override TrainerAction ChooseAction(BattleState state)
        {
            return TrainerAction.UseMove;
        }
    }

    // Child class — Inheritance from Trainer
    // Strategy Pattern — concrete strategy: scripted type-advantage logic
    public class AITrainer : Trainer
    {
        private int   _difficulty;
        private int   _selectedMoveIndex;

        public AITrainer(string name, int difficulty = 1) : base(name)
        {
            _difficulty        = difficulty;
            _selectedMoveIndex = 0;
        }

        public int SelectedMoveIndex => _selectedMoveIndex;

        // Overrides abstract method — Strategy Pattern / Polymorphism
        // AI strategy: loops through moves and picks best type advantage
        public override TrainerAction ChooseAction(BattleState state)
        {
            TamedMonster? active = GetActiveMonster();
            if (active == null) return TrainerAction.Forfeit;

            float bestMultiplier = -1f;
            int   bestIndex      = 0;

            for (int i = 0; i < active.MoveSet.Length; i++)
            {
                Move move = active.MoveSet[i];
                float multiplier = TypeChart.GetMultiplier(
                    move.MoveType,
                    state.PlayerMonster.PrimaryType
                );
                if (state.PlayerMonster.HasDualType)
                {
                    multiplier *= TypeChart.GetMultiplier(
                        move.MoveType,
                        state.PlayerMonster.SecondaryType
                    );
                }

                if (multiplier > bestMultiplier)
                {
                    bestMultiplier = multiplier;
                    bestIndex      = i;
                }
            }

            _selectedMoveIndex = bestIndex;
            return TrainerAction.UseMove;
        }
    }
}
