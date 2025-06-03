using UnityEngine;

namespace Data
{
    [CreateAssetMenu(fileName = "AIWeights", menuName = "Scriptable Objects/AIWeights")]
    public class AIWeights : ScriptableObject
    {
        public float SingleHit = 1.0f;
        public float AOE = 0.75f;
        public float MultiRandomHit = 2.5f;
        public float Charge = 0.6f;
        public float BuffBase = 35f;
        public float DebuffBase = 30f;
        public float ClearOppBuffBase = 40f;
        public float ClearSelfDebuffBase = 30f;
        public float StunBase = 40f;
        public float HealBase = 40f;
        public float KillBonus = 50f;
    }
}