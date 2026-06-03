using System;
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

        // Animation hooks — subscribe in GameManager to create BattleAnimation instances
        public event Action<Monster, int>?                  AttackHit;      // target, damage dealt
        public event Action<Monster, int>?                  HealUsed;       // caster, amount healed
        public event Action<Monster, StatusCondition>?      StatusApplied;
        public event Action<Monster, StatusCondition, int>? StatusDamage;
        public event Action<Monster, StatusCondition>?      TurnSkipped;    // monster, status that caused skip
        public event Action<Monster, StatusCondition>?      StatusRemoved;

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

            // Apply burn/poison damage before acting; track whether damage was logged
            bool hadStatusDamage = attacker.Status == StatusCondition.Burned
                                || attacker.Status == StatusCondition.Poisoned;
            ApplyStatusEffect(attacker);

            // Frozen: always skips exactly one turn, then clears
            if (attacker.Status == StatusCondition.Frozen)
            {
                string msg = $"{attacker.Name} is frozen solid!";
                StatusCondition prev = attacker.Status;
                attacker.Status = StatusCondition.None;
                _battleLog.Add(msg);
                TurnSkipped?.Invoke(attacker, prev);
                StatusRemoved?.Invoke(attacker, prev);
                return msg;
            }

            // Paralysed: guaranteed skip for one turn, then clears
            if (attacker.Status == StatusCondition.Paralysed)
            {
                string msg = $"{attacker.Name} is paralysed and cannot move!";
                StatusCondition prev = attacker.Status;
                attacker.Status = StatusCondition.None;
                _battleLog.Add(msg);
                TurnSkipped?.Invoke(attacker, prev);
                StatusRemoved?.Invoke(attacker, prev);
                return msg;
            }

            // Visual separator between status events and move execution
            if (hadStatusDamage) _battleLog.Add("──");

            StatusCondition defenderStatusBefore = defender.Status;
            Move   selectedMove    = attacker.MoveSet[moveIndex];
            int    hpBefore        = defender.Hp;
            int    attackerHpBefore = attacker.Hp;
            string result          = selectedMove.Execute(attacker, defender);
            int    damageDealt     = Math.Max(0, hpBefore - defender.Hp);
            int    healAmount      = Math.Max(0, attacker.Hp - attackerHpBefore);
            if (damageDealt > 0) AttackHit?.Invoke(defender, damageDealt);
            if (healAmount  > 0) HealUsed?.Invoke(attacker, healAmount);
            _battleLog.Add(result);
            if (defender.Status != defenderStatusBefore && defender.Status != StatusCondition.None)
                StatusApplied?.Invoke(defender, defender.Status);
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

            // Apply burn/poison damage before acting
            bool hadStatusDamage = attacker.Status == StatusCondition.Burned
                                || attacker.Status == StatusCondition.Poisoned;
            ApplyStatusEffect(attacker);

            // Frozen: always skips exactly one turn, then clears
            if (attacker.Status == StatusCondition.Frozen)
            {
                string msg = $"{attacker.Name} is frozen solid!";
                StatusCondition prev = attacker.Status;
                attacker.Status = StatusCondition.None;
                _battleLog.Add(msg);
                TurnSkipped?.Invoke(attacker, prev);
                StatusRemoved?.Invoke(attacker, prev);
                return msg;
            }

            // Paralysed: guaranteed skip for one turn, then clears
            if (attacker.Status == StatusCondition.Paralysed)
            {
                string msg = $"{attacker.Name} is paralysed and cannot move!";
                StatusCondition prev = attacker.Status;
                attacker.Status = StatusCondition.None;
                _battleLog.Add(msg);
                TurnSkipped?.Invoke(attacker, prev);
                StatusRemoved?.Invoke(attacker, prev);
                return msg;
            }

            // Visual separator between status events and move execution
            if (hadStatusDamage) _battleLog.Add("──");

            StatusCondition defenderStatusBefore = defender.Status;
            int    hpBefore        = defender.Hp;
            int    attackerHpBefore = attacker.Hp;
            string result          = moveset[moveIndex].Execute(attacker, defender);
            int    damageDealt     = Math.Max(0, hpBefore - defender.Hp);
            int    healAmount      = Math.Max(0, attacker.Hp - attackerHpBefore);
            if (damageDealt > 0) AttackHit?.Invoke(defender, damageDealt);
            if (healAmount  > 0) HealUsed?.Invoke(attacker, healAmount);
            _battleLog.Add(result);
            if (defender.Status != defenderStatusBefore && defender.Status != StatusCondition.None)
                StatusApplied?.Invoke(defender, defender.Status);
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
            StatusCondition status = m.Status;
            int dmg = m.ApplyStatusEffect();

            switch (status)
            {
                case StatusCondition.Burned:
                    _battleLog.Add($"{m.Name} is hurt by its burn! (-{dmg} HP)");
                    StatusDamage?.Invoke(m, status, dmg);
                    break;
                case StatusCondition.Poisoned:
                    _battleLog.Add($"{m.Name} is hurt by poison! (-{dmg} HP)");
                    StatusDamage?.Invoke(m, status, dmg);
                    break;
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
