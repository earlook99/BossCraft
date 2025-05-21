using GameSystem;
using UnityEngine;

namespace Entity
{
    /// <summary>
    /// Represents the Boss entity in a battle.
    /// Inherits from <see cref="BattleEntity"/> and can override behaviors for boss-specific mechanics.
    /// </summary>
    public class BossEntity : BattleEntity
    {
        // TODO: Write barrier logic

        /// <summary>
        /// Calculates the elemental weakness factor for the boss against a given move type.
        /// Bosses, unlike player characters, can have elemental weaknesses and resistances.
        /// </summary>
        /// <param name="moveType">The elemental type of the incoming move.</param>
        /// <returns>The elemental weakness multiplier based on the <see cref="TypeChart"/>.</returns>
        protected override float GetWeaknessFactor(ElementType moveType) => TypeChart.GetEffectiveness(moveType, this.ElementType);
    }
}
