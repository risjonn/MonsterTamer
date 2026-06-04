namespace MonsterTamer
{
    public enum MonsterType
    {
        Normal,
        Fire,
        Water,
        Grass,
        Electric,
        Flying,
        Fighting,
        Steel
    }

    public enum GameState
    {
        MainMenu,
        Overworld,
        EncounterPrompt,
        Battle,
        PartyScreen,
        CaptureScreen,
        Victory,
        GameOver
    }

    public enum TrainerAction
    {
        UseMove,
        SwapMonster,
        UseItem,
        Forfeit
    }

    public enum StatusCondition
    {
        None,
        Burned,
        Frozen,
        Paralysed,
        Poisoned
    }

    public enum CaptureResult
    {
        Caught,
        Escaped,
        Failed
    }

    public enum ItemRarity
    {
        Common,
        Rare,
        Legendary
    }

    public enum BattleResult
    {
        PlayerWon,
        PlayerFled,
        PlayerDied,
        Ongoing
    }

    public enum LogEntryType
    {
        System,        // grey   — battle start, intro, swap, flee, resistance
        PlayerMove,    // blue   — player move usage line
        EnemyMove,     // orange — enemy move usage line
        StatusDamage,  // amber  — burn/poison per-turn damage
        TurnSkip,      // yellow — frozen/paralysed skip
        Separator,     // dim    — visual divider between status and move
        TurnSeparator, // muted  — "── Turn N ──" header
        Capture,       // gold   — catch / escape / party result
        Healing,       // green  — item heal or move heal
        EffectDamage,   // red    — damage taken
        EffectParalyse, // yellow — paralysis applied
        EffectBurn,     // red-orange — burn applied
        EffectFreeze,   // ice blue   — freeze applied
        EffectPoison,   // purple     — poison applied
    }
}
