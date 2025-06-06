using UnityEngine;
using Data;
using Entity;
using static GameSystem.GameConstants.Battle;

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
                return new RawHit(0, false, false);
            }
            
            float raw = effectPower * source.Attack;
            bool isCrit = Random.value < effectCrit;
            
            if (isCrit) 
                raw *= CRITICAL_MULTIPLIER;

            int damage = Mathf.Max(MINIMUM_DAMAGE, Mathf.RoundToInt(raw));
            return new RawHit(damage, true, isCrit);
        }

        public static bool CheckStun(float chance)
        {
            return Random.value < chance;
        }
        
        public static float GetExpectedRawDamage(BattleEntity source, MoveData data)
        {
            float totalExpectedDamage = 0f;
    
            foreach (var effect in data.Effects)
            {
                if (effect.EffectType == MoveEffectType.Damage)
                {
                    float raw = effect.Power * source.Attack;
                    raw *= (1f + effect.CritChance * (CRITICAL_MULTIPLIER - 1f));
                    raw *= effect.Accuracy;
                    
                    totalExpectedDamage += raw;
                }
            }
            
            return totalExpectedDamage;
        }
    }
}