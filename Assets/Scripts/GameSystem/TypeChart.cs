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

        // 사이클 1: 자연계 - 물리적인 원소들의 순환
        private static readonly ElementType[] NatureCycle = {
            ElementType.Blaze,   // 불
            ElementType.Nature,  // 자연
            ElementType.Tide,    // 물
            ElementType.Terra    // 대지
        };
        
        // 사이클 2: 에너지계 - 에너지와 현상들의 순환
        private static readonly ElementType[] EnergyCycle = {
            ElementType.Light,   // 빛
            ElementType.Dark,    // 어둠
            ElementType.Mystic,  // 신비
            ElementType.Storm    // 폭풍
        };
        
        // 라이벌 관계 - 각 위치의 속성들끼리 서로 강함
        private static readonly (ElementType, ElementType)[] RivalPairs = {
            (ElementType.Blaze, ElementType.Light),    // 불 ↔ 빛
            (ElementType.Nature, ElementType.Dark),    // 자연 ↔ 어둠
            (ElementType.Tide, ElementType.Mystic),    // 물 ↔ 신비
            (ElementType.Terra, ElementType.Storm)     // 대지 ↔ 폭풍
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