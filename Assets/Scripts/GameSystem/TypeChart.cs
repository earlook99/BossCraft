namespace GameSystem
{
    [System.Serializable]
    public enum ElementType
    {
        Blaze,
        Tide,
        Mystic,
        Terra,
        Nature,
        Dark,
        Light,
        Storm
    }
    
    public static class TypeChart
    {
        private const float SUPER_EFFECTIVE = 2.0f;
        private const float NOT_VERY_EFFECTIVE = 0.5f;
        private const float NORMAL_EFFECTIVENESS = 1.0f;

        public static float GetEffectiveness(ElementType moveType, ElementType defenderType) 
        {
            int attackerIndex = (int)moveType;
            int defenderIndex = (int)defenderType;
            int typeCount = System.Enum.GetValues(typeof(ElementType)).Length;
        
            int distance = (defenderIndex - attackerIndex + typeCount) % typeCount;
        
            switch (distance) 
            {
                case 0: return NORMAL_EFFECTIVENESS;
                case 1: return NOT_VERY_EFFECTIVE;
                case 2: return NORMAL_EFFECTIVENESS;
                case 3: return NORMAL_EFFECTIVENESS;
                case 4: return SUPER_EFFECTIVE;
                default: return NORMAL_EFFECTIVENESS;
            }
        }
    }
}