using UnityEngine;

namespace Data
{
    [System.Serializable]
    public class MoveEffect
    {
        public MoveEffectType EffectType;
        public int Power;
        public BuffsType BuffsType;
        
        [Range(0f, 1f)]
        public float Accuracy = 1.0f;
        
        [Range(0f, 1f)]
        public float CritChance = 0.0f;
    }
}