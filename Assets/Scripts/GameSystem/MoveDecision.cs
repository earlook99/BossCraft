namespace GameSystem
{
    public readonly struct MoveDecision
    {
        public readonly int MoveIndex;
        public readonly EntityType TargetEntity;
        public readonly float UtilityScore;

        public MoveDecision(int moveIndex, EntityType targetEntity, float score)
            => (MoveIndex, TargetEntity, UtilityScore) = (moveIndex, targetEntity, score);
    }
}