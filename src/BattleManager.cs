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
        private BossMonster?   _bossTarget;  // set if battling the final boss
        private string         _lastPlayerMonsterName = "";

        public BattleManager(PlayerTrainer player, Trainer opponent)
        {
            _player     = player;
            _opponent   = opponent;
            _result     = BattleResult.Ongoing;
            _turnNumber = 1;
            _battleLog  = new List<string>();
        }

        public BattleResult   Result                  => _result;
        public List<string>   BattleLog               => _battleLog;
        public int            TurnNumber              => _turnNumber;
        public Trainer        Opponent                => _opponent;
        public string         LastPlayerMonsterName   => _lastPlayerMonsterName;
        public WildMonster?   WildTarget
        {
            get => _wildTarget;
            set => _wildTarget = value;
        }
        public BossMonster?   BossTarget
        {
            get => _bossTarget;
            set => _bossTarget = value;
        }

        // Unified enemy reference — eliminates repeated null-coalescing chains
        public Monster?       EnemyMonster =>
            (Monster?)_wildTarget ?? (Monster?)_bossTarget ?? _opponent.GetActiveMonster();

        // Executes the player's chosen move
        public string PlayerTurn(int moveIndex)
        {
            TamedMonster? attacker = _player.GetActiveMonster();
            Monster?      defender = EnemyMonster;

            if (attacker == null || defender == null)
                return "No active monsters!";

            _lastPlayerMonsterName = attacker.Name;

            if (moveIndex < 0 || moveIndex >= attacker.MoveSet.Length)
                return "Invalid move!";

            // Apply status before moving
            ApplyStatusEffect(attacker);

            // Status conditions may skip turn
            if (attacker.Status == StatusCondition.Frozen)
            {
                attacker.Status = StatusCondition.None;
                return $"{attacker.Name} thawed out and cannot move this turn!";
            }
            if (attacker.Status == StatusCondition.Paralysed &&
                new System.Random().NextDouble() < 0.5)
                return $"{attacker.Name} is paralysed and couldn't move!";

            Move   selectedMove = attacker.MoveSet[moveIndex];
            string result       = selectedMove.Execute(attacker, defender);
            _battleLog.Add(result);
            return result;
        }

        // Executes the AI opponent's turn using Strategy Pattern
        public string EnemyTurn()
        {
            TamedMonster? defender = _player.GetActiveMonster();
            if (defender == null) return "";
            _lastPlayerMonsterName = defender.Name;

            Move[]  moveset;
            int     moveIndex = 0;
            Monster attacker;

            if (_wildTarget != null)
            {
                attacker  = _wildTarget;
                moveset   = _wildTarget.MoveSet;
                moveIndex = new System.Random().Next(moveset.Length);
            }
            else if (_bossTarget != null)
            {
                _bossTarget.UpdatePhase();
                attacker = _bossTarget;
                moveset  = _bossTarget.MoveSet;
                // Type-advantage selection — boss uses smarter AI than random
                float best = -1f;
                for (int i = 0; i < moveset.Length; i++)
                {
                    float mult = TypeChart.GetMultiplier(moveset[i].MoveType, defender.PrimaryType);
                    if (defender.HasDualType)
                        mult *= TypeChart.GetMultiplier(moveset[i].MoveType, defender.SecondaryType);
                    if (mult > best) { best = mult; moveIndex = i; }
                }
            }
            else
            {
                Monster? enemyMonster = _opponent.GetActiveMonster();
                if (enemyMonster == null) return "";
                BattleState state = new BattleState(defender, enemyMonster, _turnNumber, false);
                _opponent.ChooseAction(state);
                TamedMonster? enemyActive = _opponent.GetActiveMonster();
                if (enemyActive == null) return "";
                attacker  = enemyActive;
                moveset   = enemyActive.MoveSet;
                moveIndex = (_opponent is AITrainer ai) ? ai.SelectedMoveIndex : 0;
            }

            // Apply burn/poison damage and check for skip conditions
            ApplyStatusEffect(attacker);
            if (attacker.Status == StatusCondition.Frozen)
            {
                attacker.Status = StatusCondition.None;
                string thaw = $"{attacker.Name} thawed out and lost its turn!";
                _battleLog.Add(thaw);
                return thaw;
            }
            if (attacker.Status == StatusCondition.Paralysed &&
                new System.Random().NextDouble() < 0.5)
            {
                string para = $"{attacker.Name} is paralysed and couldn't move!";
                _battleLog.Add(para);
                return para;
            }

            string result = moveset[moveIndex].Execute(attacker, defender);
            _battleLog.Add(result);
            return result;
        }

        public void Flee()
        {
            _result = BattleResult.PlayerFled;
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

            Monster? enemyActive = EnemyMonster;
            if (enemyActive == null || !enemyActive.IsAlive)
            {
                _result = BattleResult.PlayerWon;
                if (_player.GetActiveMonster() is TamedMonster winner)
                    winner.GainExperience(_bossTarget != null ? 500 : 50 * _turnNumber);
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
