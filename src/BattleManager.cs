using System.Collections.Generic;

namespace MonsterTamer
{
    public class BattleManager
    {
        private PlayerTrainer  _player;
        private Trainer        _opponent;
        private BattleResult   _result;
        private int            _turnNumber;
        private List<string>   _battleLog;
        private WildMonster?   _wildTarget;  // set if battling a wild monster

        public BattleManager(PlayerTrainer player, Trainer opponent)
        {
            _player     = player;
            _opponent   = opponent;
            _result     = BattleResult.Ongoing;
            _turnNumber = 1;
            _battleLog  = new List<string>();
        }

        public BattleResult   Result     => _result;
        public List<string>   BattleLog  => _battleLog;
        public int            TurnNumber => _turnNumber;
        public Trainer        Opponent   => _opponent;
        public WildMonster?   WildTarget
        {
            get => _wildTarget;
            set => _wildTarget = value;
        }

        // Executes the player's chosen move
        public string PlayerTurn(int moveIndex)
        {
            TamedMonster? attacker = _player.GetActiveMonster();
            Monster?      defender = _opponent.GetActiveMonster() ?? (Monster?)_wildTarget;

            if (attacker == null || defender == null)
                return "No active monsters!";

            if (moveIndex < 0 || moveIndex >= attacker.MoveSet.Length)
                return "Invalid move!";

            // Apply status before moving
            ApplyStatusEffect(attacker);

            // Frozen/Paralysed may skip turn
            if (attacker.Status == StatusCondition.Frozen)
            {
                attacker.Status = StatusCondition.None;
                return $"{attacker.Name} is frozen and cannot move!";
            }

            Move   selectedMove = attacker.MoveSet[moveIndex];
            string result       = selectedMove.Execute(attacker, defender);
            _battleLog.Add(result);
            return result;
        }

        // Executes the AI opponent's turn using Strategy Pattern
        public string EnemyTurn()
        {
            Monster?      attacker = _opponent.GetActiveMonster() ?? (Monster?)_wildTarget;
            TamedMonster? defender = _player.GetActiveMonster();

            if (attacker == null || defender == null)
                return "";

            BattleState state = new BattleState(
                defender, attacker, _turnNumber, false
            );

            Move[] moveset;
            int    moveIndex = 0;

            if (_wildTarget != null)
            {
                moveset   = _wildTarget.MoveSet;
                moveIndex = new System.Random().Next(moveset.Length);
            }
            else
            {
                _opponent.ChooseAction(state);
                TamedMonster? enemyActive = _opponent.GetActiveMonster();
                if (enemyActive == null) return "";
                moveset   = enemyActive.MoveSet;
                moveIndex = (_opponent is AITrainer ai) ? ai.SelectedMoveIndex : 0;
                attacker  = enemyActive;
            }

            string result = moveset[moveIndex].Execute(attacker, defender);
            _battleLog.Add(result);
            return result;
        }

        // Attempts to catch the wild monster
        public CaptureResult AttemptCapture(CaptureOrb orb)
        {
            if (_wildTarget == null) return CaptureResult.Failed;
            CaptureResult result = orb.AttemptCapture(_wildTarget);
            _battleLog.Add(result == CaptureResult.Caught
                ? $"{_wildTarget.Name} was caught!"
                : result == CaptureResult.Escaped
                    ? $"{_wildTarget.Name} broke free!"
                    : "The orb missed!");
            return result;
        }

        public bool IsBattleOver()
        {
            TamedMonster? playerActive = _player.GetActiveMonster();
            if (playerActive == null || !playerActive.IsAlive)
            {
                _result = BattleResult.PlayerDied;
                return true;
            }

            Monster? enemyActive = _opponent.GetActiveMonster() ?? (Monster?)_wildTarget;
            if (enemyActive == null || !enemyActive.IsAlive)
            {
                _result = BattleResult.PlayerWon;
                // Grant experience
                if (_player.GetActiveMonster() is TamedMonster winner)
                    winner.GainExperience(50 * _turnNumber);
                return true;
            }

            _turnNumber++;
            return false;
        }

        private void ApplyStatusEffect(Monster m)
        {
            if (m.Status != StatusCondition.None)
            {
                m.ApplyStatusEffect();
                _battleLog.Add($"{m.Name} is affected by {m.Status}!");
            }
        }

        public void AddLog(string message)
        {
            _battleLog.Add(message);
        }

        public void ClearLog()
        {
            _battleLog.Clear();
        }
    }
}
