namespace MonsterTamer
{
    // Snapshot of the current battle state
    // Passed to Trainer.ChooseAction() so AI can make informed decisions
    public class BattleState
    {
        public Monster  PlayerMonster  { get; }
        public Monster  EnemyMonster   { get; }
        public int      TurnNumber     { get; }
        public bool     IsPlayerTurn   { get; }

        public BattleState(Monster playerMonster, Monster enemyMonster,
                           int turnNumber, bool isPlayerTurn)
        {
            PlayerMonster = playerMonster;
            EnemyMonster  = enemyMonster;
            TurnNumber    = turnNumber;
            IsPlayerTurn  = isPlayerTurn;
        }
    }
}
