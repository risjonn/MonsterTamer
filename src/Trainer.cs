namespace MonsterTamer
{
    // Abstract base class — Strategy Pattern
    // ChooseAction() is the strategy interface
    // PlayerTrainer and AITrainer are the concrete strategies
    public abstract class Trainer
    {
        private string    _name;
        private Party     _party;
        private Inventory _bag;

        protected Trainer(string name)
        {
            _name  = name;
            _party = new Party();
            _bag   = new Inventory();
        }

        // Properties
        public string    Name  => _name;
        public Party     Party => _party;
        public Inventory Bag   => _bag;

        // Concrete method — shared by all trainers
        public TamedMonster? GetActiveMonster()
        {
            return _party.GetActiveMonster();
        }

        // Abstract method — Strategy Pattern interface
        // PlayerTrainer reads keyboard input
        // AITrainer uses type-advantage logic
        public abstract TrainerAction ChooseAction(BattleState state);
    }
}
