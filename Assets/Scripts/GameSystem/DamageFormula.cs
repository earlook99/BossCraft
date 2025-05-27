using UnityEngine;
using Data;
using Entity;

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
        public static RawHit GetRawHit(BattleEntity source, int effectPower, float effectAcc, float effectCrit)
        {
            if (Random.value > effectAcc)
            {
                return new RawHit(0, false, false); // miss
            }
            float raw = effectPower * source.Attack;
            bool isCrit = (Random.value < effectCrit);
            if (isCrit) raw *= 1.5f;

            int dmg = Mathf.Max(1, Mathf.RoundToInt(raw));
            return new RawHit(dmg, true, isCrit);
        }

        // 스턴 등 상태이상 판정
        public static bool CheckStun(float chance)
        {
            return (Random.value < chance);
        }
        
        public static float GetExpectedRawDamage(BattleEntity source, MoveData data)
        {
            float totalExpectedDamage = 0f;
    
            foreach (var eff in data.Effects)
            {
                if (eff.EffectType == MoveEffectType.Damage)
                {
                    float effPower = eff.Power;
                    float effAcc = eff.Accuracy;
                    float effCrit = eff.CritChance;
                    
                    float raw = effPower * source.Attack;
                    
                    raw *= (1f + effCrit * 0.5f);
                    
                    raw *= effAcc;
                    
                    totalExpectedDamage += raw;
                }
            }
            
            return totalExpectedDamage;
        }
    }
}