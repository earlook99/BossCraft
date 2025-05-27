using UnityEngine;

namespace Data
{
    /// <summary>
    /// ScriptableObject defining weights for AI decision-making.
    /// These weights are used by the Boss AI to score potential moves.
    /// </summary>
    [CreateAssetMenu(fileName = "AIWeights", menuName = "Scriptable Objects/AIWeights")]
    public class AIWeights : ScriptableObject
    {
        // Damaging move factor
        /// <summary>Factor for scoring single-target damaging moves.</summary>
        public float SingleHit = 1.0f;
        /// <summary>Factor for scoring Area of Effect (AOE) damaging moves.</summary>
        public float AOE = 0.75f;
        /// <summary>Factor for scoring multi-hit random-target damaging moves.</summary>
        public float MultiRandomHit = 2.5f;
        /// <summary>Factor for scoring charge-up or delayed damaging moves.</summary>
        public float Charge = 0.6f;
        
        // Support move base
        /// <summary>Base score for moves that apply buffs.</summary>
        public float BuffBase = 35f;
        /// <summary>Base score for moves that apply debuffs.</summary>
        public float DebuffBase = 30f;
        /// <summary>Base score for moves that clear opponent buffs.</summary>
        public float ClearOppBuffBase = 40f;
        /// <summary>Base score for moves that clear self debuffs.</summary>
        public float ClearSelfDebuffBase = 30f;
        
        // Status conditions base
        /// <summary>Base score for moves that can apply a stun effect.</summary>
        public float StunBase = 40f;

        public float HealBase = 40f;
        
        // Kill bonus
        /// <summary>Bonus score added if a move is predicted to knock out a target.</summary>
        public float KillBonus = 50f;
    }
}
