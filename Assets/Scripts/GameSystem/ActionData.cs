namespace GameSystem
{
    /// <summary>
    /// Represents the data for an action to be performed in battle.
    /// </summary>
    public struct ActionData
    {
        /// <summary>
        /// The type of action to perform (e.g., Move, Item).
        /// </summary>
        public ActionType Action;
        /// <summary>
        /// The index of the specific action (e.g., index of a move in a moveset, or index of an item).
        /// </summary>
        public int ActionIndex;
        /// <summary>
        /// The entity performing the action.
        /// </summary>
        public EntityType Source;
        /// <summary>
        /// The entity targeted by the action.
        /// </summary>
        public EntityType Target;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActionData"/> struct.
        /// </summary>
        /// <param name="actionType">The type of action.</param>
        /// <param name="actionIndex">The index of the action.</param>
        /// <param name="source">The source entity.</param>
        /// <param name="target">The target entity.</param>
        public ActionData(ActionType actionType, int actionIndex, EntityType source, EntityType target)
        {
            Action = actionType;
            ActionIndex = actionIndex;
            Source = source;
            Target = target;
        }
    }
}
