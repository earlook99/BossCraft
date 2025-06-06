namespace GameSystem
{
    public struct ActionData
    {
        public ActionType Action;
        public int ActionIndex;
        public EntityType Source;
        public EntityType Target;

        public ActionData(ActionType actionType, int actionIndex, EntityType source, EntityType target)
        {
            Action = actionType;
            ActionIndex = actionIndex;
            Source = source;
            Target = target;
        }
    }
}