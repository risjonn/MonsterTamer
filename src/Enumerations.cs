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
}
