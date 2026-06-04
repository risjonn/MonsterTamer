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
        private List<LogEntry> _battleLog;
        private int            _lastTurnSeparatorFor;
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
            _result                = BattleResult.Ongoing;
            _turnNumber            = 1;
            _battleLog             = new List<LogEntry>();
            _lastTurnSeparatorFor  = 0;
        }

        public BattleResult    Result                  => _result;
        public List<LogEntry>  BattleLog               => _battleLog;
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

            AddTurnSeparatorIfNew();

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
                _battleLog.Add(new LogEntry($"~ {msg}", LogEntryType.TurnSkip));
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
                _battleLog.Add(new LogEntry($"~ {msg}", LogEntryType.TurnSkip));
                TurnSkipped?.Invoke(attacker, prev);
                StatusRemoved?.Invoke(attacker, prev);
                return msg;
            }

            // Visual separator between status events and move execution
            if (hadStatusDamage) _battleLog.Add(new LogEntry("──", LogEntryType.Separator));

            StatusCondition defenderStatusBefore = defender.Status;
            Move   selectedMove     = attacker.MoveSet[moveIndex];
            int    hpBefore         = defender.Hp;
            int    attackerHpBefore = attacker.Hp;

            _battleLog.Add(new LogEntry($"> {attacker.Name} used {selectedMove.Name}!", LogEntryType.PlayerMove));
            string result     = selectedMove.Execute(attacker, defender);
            int    damageDealt = Math.Max(0, hpBefore - defender.Hp);
            int    healAmount  = Math.Max(0, attacker.Hp - attackerHpBefore);
            if (damageDealt > 0) AttackHit?.Invoke(defender, damageDealt);
            if (healAmount  > 0) HealUsed?.Invoke(attacker, healAmount);
            _battleLog.Add(new LogEntry($"* {result}", ResolveEffectType(damageDealt, healAmount, defenderStatusBefore, defender.Status)));
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
                _battleLog.Add(new LogEntry($"~ {msg}", LogEntryType.TurnSkip));
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
                _battleLog.Add(new LogEntry($"~ {msg}", LogEntryType.TurnSkip));
                TurnSkipped?.Invoke(attacker, prev);
                StatusRemoved?.Invoke(attacker, prev);
                return msg;
            }

            // Visual separator between status events and move execution
            if (hadStatusDamage) _battleLog.Add(new LogEntry("──", LogEntryType.Separator));

            StatusCondition defenderStatusBefore = defender.Status;
            Move   activeMove       = moveset[moveIndex];
            int    hpBefore         = defender.Hp;
            int    attackerHpBefore = attacker.Hp;

            _battleLog.Add(new LogEntry($"< {attacker.Name} used {activeMove.Name}!", LogEntryType.EnemyMove));
            string result      = activeMove.Execute(attacker, defender);
            int    damageDealt = Math.Max(0, hpBefore - defender.Hp);
            int    healAmount  = Math.Max(0, attacker.Hp - attackerHpBefore);
            if (damageDealt > 0) AttackHit?.Invoke(defender, damageDealt);
            if (healAmount  > 0) HealUsed?.Invoke(attacker, healAmount);
            _battleLog.Add(new LogEntry($"* {result}", ResolveEffectType(damageDealt, healAmount, defenderStatusBefore, defender.Status)));
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
            AddTurnSeparatorIfNew();
            CaptureResult result = orb.AttemptCapture(_wildTarget);
            string msg = result == CaptureResult.Caught
                ? $"* {_wildTarget.Name} was caught!"
                : result == CaptureResult.Escaped
                    ? $"* {_wildTarget.Name} broke free!"
                    : "* The orb missed!";
            _battleLog.Add(new LogEntry(msg, LogEntryType.Capture));
            return result;
        }

        // isEndOfRound = true only after the enemy half-turn so the turn counter
        // advances exactly once per complete round (player action + enemy action).
        public bool IsBattleOver(bool isEndOfRound = false)
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

            if (isEndOfRound) _turnNumber++;
            return false;
        }

        private void ApplyStatusEffect(Monster m)
        {
            StatusCondition status = m.Status;
            int dmg = m.ApplyStatusEffect();

            switch (status)
            {
                case StatusCondition.Burned:
                    _battleLog.Add(new LogEntry($"~ {m.Name} is hurt by its burn! (-{dmg} HP)", LogEntryType.StatusDamage));
                    StatusDamage?.Invoke(m, status, dmg);
                    break;
                case StatusCondition.Poisoned:
                    _battleLog.Add(new LogEntry($"~ {m.Name} is hurt by poison! (-{dmg} HP)", LogEntryType.StatusDamage));
                    StatusDamage?.Invoke(m, status, dmg);
                    break;
            }
        }

        private static LogEntryType ResolveEffectType(int damageDealt, int healAmount, StatusCondition before, StatusCondition after)
        {
            if (damageDealt > 0) return LogEntryType.EffectDamage;
            if (healAmount  > 0) return LogEntryType.Healing;
            if (after != before && after != StatusCondition.None)
                return after switch
                {
                    StatusCondition.Paralysed => LogEntryType.EffectParalyse,
                    StatusCondition.Burned    => LogEntryType.EffectBurn,
                    StatusCondition.Frozen    => LogEntryType.EffectFreeze,
                    StatusCondition.Poisoned  => LogEntryType.EffectPoison,
                    _                         => LogEntryType.System,
                };
            return LogEntryType.System;
        }

        public void AddLog(string message, LogEntryType type = LogEntryType.System)
        {
            _battleLog.Add(new LogEntry(message, type));
        }

        public void AddTurnSeparatorIfNew()
        {
            if (_turnNumber == _lastTurnSeparatorFor) return;
            _battleLog.Add(new LogEntry($"-- Turn {_turnNumber} --", LogEntryType.TurnSeparator));
            _lastTurnSeparatorFor = _turnNumber;
        }

        public void ClearLog()
        {
            _battleLog.Clear();
            _lastTurnSeparatorFor = 0;
        }
    }
}
