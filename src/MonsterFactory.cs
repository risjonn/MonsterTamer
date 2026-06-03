namespace MonsterTamer
{
    // Factory Pattern
    // Centralises creation of all Sarawak monsters
    // Caller never needs to know constructor details
    public static class MonsterFactory
    {
        public static TamedMonster Tame(WildMonster monster)
        {
            return monster.HasDualType
                ? new TamedMonster(
                    monster.Name, monster.MaxHp, monster.Level,
                    monster.PrimaryType, monster.SecondaryType,
                    monster.GetBattleCry(), monster.GetDescription(),
                    monster.MoveSet)
                : new TamedMonster(
                    monster.Name, monster.MaxHp, monster.Level,
                    monster.PrimaryType,
                    monster.GetBattleCry(), monster.GetDescription(),
                    monster.MoveSet);
        }

        public static TamedMonster CreateKelabit(int level = 12)
        {
            return new TamedMonster(
                "Kelabit", 95, level,
                MonsterType.Flying, MonsterType.Grass,
                "KRAAK!",
                "Rhinoceros Hornbill  |  Flying / Grass",
                new Move[]
                {
                    new AttackMove("Casque Crash",  65, MonsterType.Flying),
                    new AttackMove("Gust Strike",   40, MonsterType.Flying),
                    new HealMove  ("Forest Mend",   30, MonsterType.Grass),
                    new StatusMove("Feather Blind", StatusCondition.Paralysed, MonsterType.Flying),
                }
            );
        }

        public static TamedMonster CreateBekantanTamed(int level = 8) => Tame(CreateBekantan(level));
        public static TamedMonster CreateMawasTamed(int level = 10) => Tame(CreateMawas(level));
        public static TamedMonster CreateRajahTamed(int level = 9) => Tame(CreateRajah(level));
        public static TamedMonster CreateEnggangTamed(int level = 11) => Tame(CreateEnggang(level));
        public static TamedMonster CreatePelandukTamed(int level = 6) => Tame(CreatePelanduk(level));
        public static TamedMonster CreateTemajuTamed(int level = 13) => Tame(CreateTemaju(level));
        public static TamedMonster CreateBuayaTamed(int level = 14) => Tame(CreateBuaya(level));

        public static WildMonster CreateBekantan(int level = 8)
        {
            return new WildMonster(
                "Bekantan", 80, level,
                MonsterType.Normal, MonsterType.Water,
                "OOH-OOH!",
                "Proboscis Monkey  |  Normal / Water",
                new Move[]
                {
                    new AttackMove("Riverleap",  50, MonsterType.Water),
                    new AttackMove("Nose Slam",  45, MonsterType.Normal),
                    new StatusMove("Screech", StatusCondition.Paralysed, MonsterType.Normal),
                    new HealMove  ("River Soak", 20, MonsterType.Water),
                },
                catchRate: 0.55f
            );
        }

        public static WildMonster CreateMawas(int level = 10)
        {
            return new WildMonster(
                "Mawas", 110, level,
                MonsterType.Normal, MonsterType.Fighting,
                "GRAAAH!",
                "Bornean Orangutan  |  Normal / Fighting",
                new Move[]
                {
                    new AttackMove("Vine Slam",    60, MonsterType.Fighting),
                    new AttackMove("Branch Smash", 55, MonsterType.Normal),
                    new StatusMove("War Cry", StatusCondition.Burned, MonsterType.Fighting),
                    new HealMove  ("Jungle Rest",  25, MonsterType.Grass),
                },
                catchRate: 0.45f
            );
        }

        public static WildMonster CreateRajah(int level = 9)
        {
            return new WildMonster(
                "Rajah", 70, level,
                MonsterType.Electric, MonsterType.Flying,
                "BZZZT!",
                "Rajah Brooke's Birdwing  |  Electric / Flying",
                new Move[]
                {
                    new AttackMove("Wing Spark",    55, MonsterType.Electric),
                    new AttackMove("Aerial Zap",    45, MonsterType.Electric),
                    new StatusMove("Static Dust", StatusCondition.Paralysed, MonsterType.Electric),
                    new HealMove  ("Wind Drift",    20, MonsterType.Flying),
                },
                catchRate: 0.50f
            );
        }

        public static WildMonster CreateEnggang(int level = 11)
        {
            return new WildMonster(
                "Enggang", 100, level,
                MonsterType.Fighting, MonsterType.Grass,
                "ROOAAR!",
                "Sun Bear  |  Fighting / Grass",
                new Move[]
                {
                    new AttackMove("Honey Rage",   60, MonsterType.Fighting),
                    new AttackMove("Claw Swipe",   50, MonsterType.Normal),
                    new StatusMove("Bear Growl", StatusCondition.Frozen, MonsterType.Normal),
                    new HealMove  ("Honey Lick",   30, MonsterType.Grass),
                },
                catchRate: 0.40f
            );
        }

        public static WildMonster CreatePelanduk(int level = 6)
        {
            return new WildMonster(
                "Pelanduk", 60, level,
                MonsterType.Normal,
                "pip pip!",
                "Lesser Mouse Deer  |  Normal",
                new Move[]
                {
                    new AttackMove("Phantom Step", 40, MonsterType.Normal),
                    new AttackMove("Quick Jab",    35, MonsterType.Normal),
                    new StatusMove("Timid Gaze", StatusCondition.Paralysed, MonsterType.Normal),
                    new HealMove  ("Forest Rest",  25, MonsterType.Grass),
                },
                catchRate: 0.70f
            );
        }

        public static WildMonster CreateTemaju(int level = 13)
        {
            return new WildMonster(
                "Temaju", 90, level,
                MonsterType.Steel, MonsterType.Normal,
                "...",
                "Sunda Pangolin  |  Ground / Steel",
                new Move[]
                {
                    new AttackMove("Scale Shield", 55, MonsterType.Steel),
                    new AttackMove("Curl Bash",    50, MonsterType.Steel),
                    new StatusMove("Iron Dust", StatusCondition.Poisoned, MonsterType.Steel),
                    new HealMove  ("Shell Rest",   30, MonsterType.Normal),
                },
                catchRate: 0.35f
            );
        }

        public static WildMonster CreateBuaya(int level = 14)
        {
            return new WildMonster(
                "Buaya", 120, level,
                MonsterType.Water, MonsterType.Normal,
                "HISSS!",
                "Saltwater Crocodile  |  Water / Ground",
                new Move[]
                {
                    new AttackMove("Death Roll",   70, MonsterType.Water),
                    new AttackMove("Tail Whip",    55, MonsterType.Normal),
                    new StatusMove("Swamp Mist", StatusCondition.Poisoned, MonsterType.Water),
                    new HealMove  ("River Dive",   25, MonsterType.Water),
                },
                catchRate: 0.30f
            );
        }

        public static BossMonster CreateNagaBelian(int level = 30)
        {
            return new BossMonster(
                "Naga Belian", 400, level,
                MonsterType.Water, MonsterType.Normal,
                "THE RIVER SPIRIT AWAKENS!",
                new Move[]
                {
                    new AttackMove("Tidal Surge",   80, MonsterType.Water),
                    new AttackMove("Spirit Lash",   70, MonsterType.Normal),
                    new StatusMove("Cursed Mist", StatusCondition.Poisoned, MonsterType.Water),
                    new HealMove  ("Ancient Tide",  50, MonsterType.Water),
                },
                totalPhases: 3
            );
        }
    }
}
