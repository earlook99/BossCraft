namespace GameSystem
{
    [System.Serializable]
    public enum ElementType
    {
        Fire, Water, Electric, Rock, Grass
    }
    
    public static class TypeChart
    {
        public static float GetEffectiveness(ElementType moveType, ElementType defenderType) 
        {
            int attackerIndex = (int)moveType;
            int defenderIndex = (int)defenderType;
            int typeCount = System.Enum.GetValues(typeof(ElementType)).Length;
        
            int distance = (defenderIndex - attackerIndex + typeCount) % typeCount;
        
            switch (distance) 
            {
                case 0: return 1.0f;
                case 1: return 2.0f;
                case 2: return 1.0f;
                case 3: return 1.0f; 
                case 4: return 0.5f;
                default: return 1.0f;
            }
        }
    }
}
