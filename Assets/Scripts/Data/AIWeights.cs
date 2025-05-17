using UnityEngine;

namespace Data
{
    [CreateAssetMenu(fileName = "AIWeights", menuName = "Scriptable Objects/AIWeights")]
    public class AIWeights : ScriptableObject
    {
        // Damaging move factor
        public readonly float SingleHit = 1.0f;
        public float AOE = 0.75f;
        public float MultiRandomHit = 2.5f;
        public float Charge = 0.6f;
        
        // Support move base
        public float BuffBase = 35f;
        public float DebuffBase = 30f;
        public float ClearOppBuffBase = 40f;
        public float ClearSelfDebuffBase = 30f;
        
        // Status conditions base
        public float StunBase = 40f;
        
        // Kill bonus
        public float KillBonus = 50f;
    }
}
