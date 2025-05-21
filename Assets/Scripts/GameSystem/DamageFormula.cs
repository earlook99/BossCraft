using Data;
using Entity;
using UnityEngine;

namespace GameSystem
{
    /// <summary>
    /// Represents the raw result of a damage calculation attempt, before mitigation.
    /// </summary>
    public readonly struct RawHit
    {
        /// <summary>
        /// Gets the calculated raw damage amount. 
        /// This is 0 if the attack missed.
        /// </summary>
        public int Damage { get; }
        /// <summary>
        /// Gets a value indicating whether the attack successfully hit the target.
        /// </summary>
        public bool IsHit { get; }
        /// <summary>
        /// Gets a value indicating whether the attack was a critical hit.
        /// </summary>
        public bool IsCritical { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RawHit"/> struct.
        /// </summary>
        /// <param name="damage">The raw damage calculated.</param>
        /// <param name="isHit">Whether the attack landed.</param>
        /// <param name="isCritical">Whether the attack was a critical hit.</param>
        public RawHit(int damage, bool isHit, bool isCritical)
        {
            Damage = damage;
            IsHit = isHit;
            IsCritical = isCritical;
        }
    }
    
    /// <summary>
    /// Provides static methods for calculating damage and hit outcomes.
    /// </summary>
    public static class DamageFormula
    {
        /// <summary>
        /// Calculates the raw damage, hit status, and critical status of a move.
        /// </summary>
        /// <param name="source">The entity performing the move.</param>
        /// <param name="move">The move instance being used.</param>
        /// <returns>A <see cref="RawHit"/> struct containing the outcome.</returns>
        public static RawHit GetRawHit(BattleEntity source, MoveInstance move)
        {
            MoveData data = move.Data;

            if (UnityEngine.Random.value > data.Accuracy)
            {
                return new RawHit(0, false, false);
            }

            float raw = data.Power * source.Attack;

            bool isCritical = UnityEngine.Random.value < data.CriticalChance;
            if (isCritical)
            {
                raw *= 1.5f;
            }

            return new RawHit(Mathf.Max(1, Mathf.RoundToInt(raw)), true, isCritical);
        }
        
        /// <summary>
        /// Calculates the expected raw damage of a move, factoring in accuracy and critical chance.
        /// This is useful for AI decision-making.
        /// </summary>
        /// <param name="source">The entity that would perform the move.</param>
        /// <param name="data">The data of the move to calculate expected damage for.</param>
        /// <returns>The average expected raw damage as a float.</returns>
        public static float GetExpectedRawDamage(BattleEntity source, MoveData data)
        {
            float raw = data.Power * source.Attack;
            raw *= (1f + data.CriticalChance * 0.5f);
            raw *= data.Accuracy;
            return raw;
        }
    }
}
