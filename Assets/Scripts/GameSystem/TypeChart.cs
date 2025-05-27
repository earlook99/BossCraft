namespace GameSystem
{
    /// <summary>
    /// Defines the elemental types used in the game for moves and entities.
    /// </summary>
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
    
    /// <summary>
    /// Provides methods to determine type effectiveness based on a predefined chart.
    /// The type chart follows a simple cycle: Water > Fire > Grass > Rock > Electric > Water.
    /// </summary>
    public static class TypeChart
    {
        /// <summary>
        /// Calculates the effectiveness multiplier of a move type against a defender's type.
        /// </summary>
        /// <param name="moveType">The elemental type of the attacking move.</param>
        /// <param name="defenderType">The elemental type of the defending entity.</param>
        /// <returns>
        /// A float representing the damage multiplier:
        /// - 2.0f for super effective.
        /// - 0.5f for not very effective.
        /// - 1.0f for normal effectiveness.
        /// </returns>
        public static float GetEffectiveness(ElementType moveType, ElementType defenderType) 
        {
            int attackerIndex = (int)moveType;
            int defenderIndex = (int)defenderType;
            int typeCount = System.Enum.GetValues(typeof(ElementType)).Length;
        
            int distance = (defenderIndex - attackerIndex + typeCount) % typeCount;
        
            switch (distance) 
            {
                case 0: return 1.0f;
                case 1: return 0.5f;
                case 2: return 1.0f;
                case 3: return 1.0f; 
                case 4: return 2.0f;
                default: return 1.0f;
            }
        }
    }
}
