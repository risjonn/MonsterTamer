namespace MonsterTamer
{
    // Type advantage lookup table
    // Returns damage multiplier based on attacking and defending types
    public static class TypeChart
    {
        // Rows = attacking type, Columns = defending type
        // Order matches MonsterType enum: Normal, Fire, Water, Grass, Electric, Flying, Fighting, Steel
        private static readonly float[,] _chart = new float[8, 8]
        {
            //          Nrm   Fire  Watr  Grss  Elec  Fly   Fgt   Stl
            /* Nrm */  {1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 0.5f},
            /* Fire */ {1.0f, 0.5f, 0.5f, 2.0f, 1.0f, 1.0f, 1.0f, 2.0f},
            /* Watr */ {1.0f, 2.0f, 0.5f, 0.5f, 1.0f, 1.0f, 1.0f, 1.0f},
            /* Grss */ {1.0f, 0.5f, 2.0f, 0.5f, 1.0f, 0.5f, 1.0f, 0.5f},
            /* Elec */ {1.0f, 1.0f, 2.0f, 0.5f, 0.5f, 2.0f, 1.0f, 1.0f},
            /* Fly  */ {1.0f, 1.0f, 1.0f, 2.0f, 0.5f, 1.0f, 2.0f, 0.5f},
            /* Fgt  */ {2.0f, 1.0f, 1.0f, 1.0f, 1.0f, 0.5f, 1.0f, 2.0f},
            /* Stl  */ {1.0f, 0.5f, 1.0f, 2.0f, 1.0f, 1.0f, 1.0f, 0.5f},
        };

        public static float GetMultiplier(MonsterType attackType, MonsterType defendType)
        {
            return _chart[(int)attackType, (int)defendType];
        }
    }
}
