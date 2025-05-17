using Data;
using Entity;
using UnityEngine;

namespace GameSystem
{
    public readonly struct RawHit
    {
        public int Damage { get; }
        public bool IsHit { get; }
        public bool IsCritical { get; }

        public RawHit(int damage, bool isHit, bool isCritical)
        {
            Damage = damage;
            IsHit = isHit;
            IsCritical = isCritical;
        }
    }
    
    public static class DamageFormula
    {
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
        
        public static float GetExpectedRawDamage(BattleEntity source, MoveData data)
        {
            float raw = data.Power * source.Attack;
            raw *= (1f + data.CriticalChance * 0.5f);
            raw *= data.Accuracy;
            return raw;
        }
    }
}
