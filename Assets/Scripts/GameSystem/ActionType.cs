namespace GameSystem
{
    /// <summary>
    /// Defines the types of actions a battle entity can perform.
    /// </summary>
    public enum ActionType
    {
        /// <summary>
        /// Perform a move/skill.
        /// </summary>
        Move,
        /// <summary>
        /// Use an item.
        /// </summary>
        Item,
        /// <summary>
        /// Take a defensive stance.
        /// </summary>
        Guard,
        /// <summary>
        /// Taunt the opponent.
        /// </summary>
        Taunt
    }
}
