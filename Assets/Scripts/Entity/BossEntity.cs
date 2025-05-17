using GameSystem;
using UnityEngine;

namespace Entity
{
    public class BossEntity : BattleEntity
    {
        // TODO: Write barrier logic

        protected override float GetWeaknessFactor(ElementType moveType) => TypeChart.GetEffectiveness(moveType, this.ElementType);
    }
}
