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

        // 자연계 순환: 불 → 자연 → 물 → 대지 → 불
        private static readonly ElementType[] NatureCycle = {
            ElementType.Blaze,
            ElementType.Nature,
            ElementType.Tide,
            ElementType.Terra
        };
        
        // 에너지계 순환: 빛 → 어둠 → 신비 → 폭풍 → 빛
        private static readonly ElementType[] EnergyCycle = {
            ElementType.Light,
            ElementType.Dark,
            ElementType.Mystic,
            ElementType.Storm
        };
        
        // 라이벌 관계: 서로 강함
        private static readonly (ElementType, ElementType)[] RivalPairs = {
            (ElementType.Blaze, ElementType.Light),
            (ElementType.Nature, ElementType.Dark),
            (ElementType.Tide, ElementType.Mystic),
            (ElementType.Terra, ElementType.Storm)
        };

        public static float GetEffectiveness(ElementType moveType, ElementType defenderType)
        {
            if (moveType == defenderType) 
                return NORMAL_EFFECTIVENESS;
            
            foreach (var (type1, type2) in RivalPairs)
            {
                if ((moveType == type1 && defenderType == type2) || 
                    (moveType == type2 && defenderType == type1))
                {
                    return SUPER_EFFECTIVE;
                }
            }
            
            float cycleEffectiveness = CheckCycleEffectiveness(moveType, defenderType, NatureCycle);
            if (cycleEffectiveness != NORMAL_EFFECTIVENESS)
                return cycleEffectiveness;
                
            cycleEffectiveness = CheckCycleEffectiveness(moveType, defenderType, EnergyCycle);
            if (cycleEffectiveness != NORMAL_EFFECTIVENESS)
                return cycleEffectiveness;
            
            return NORMAL_EFFECTIVENESS;
        }
        
        private static float CheckCycleEffectiveness(ElementType moveType, ElementType defenderType, ElementType[] cycle)
        {
            int moveIndex = System.Array.IndexOf(cycle, moveType);
            int defenderIndex = System.Array.IndexOf(cycle, defenderType);
            
            if (moveIndex == -1 || defenderIndex == -1)
                return NORMAL_EFFECTIVENESS;
            
            int nextIndex = (moveIndex + 1) % cycle.Length;
            if (defenderIndex == nextIndex)
                return SUPER_EFFECTIVE;
            
            int prevIndex = (moveIndex - 1 + cycle.Length) % cycle.Length;
            if (defenderIndex == prevIndex)
                return NOT_VERY_EFFECTIVE;
            
            return NORMAL_EFFECTIVENESS;
        }
    }
}