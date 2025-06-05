using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using AI;
using GameSystem;
using UnityEngine;
using Data;

namespace Entity
{
    public class BossEntity : BattleEntity
    {
        [Header("Shield System")]
        [SerializeField] private ShieldPattern shieldPattern;
        
        private int shieldHP = 0;
        private int shieldStacks = 0;
        private int maxShieldStacks = 3;
        private int lastDamageTurn = -1;
        
        private BattleManager battleManager;
        
        private const float SHIELD_DAMAGE_REDUCTION = 0.5f;
        private const int SHIELD_BREAK_STUN_DURATION = 1;

        public bool HasShield => shieldHP > 0;
        public int ShieldHP => shieldHP;
        public int ShieldStacks => shieldStacks;
        public int MaxShieldStacks => maxShieldStacks;
        public ShieldPattern ShieldPattern => shieldPattern;

        protected override void Start()
        {
            base.Start();
            
            battleManager = FindAnyObjectByType<BattleManager>();
    
            if (shieldPattern != null)
            {
                shieldPattern.ResetTriggers();
            }
        }
        
        public ShieldTrigger GetAvailableShieldTrigger(int currentTurn)
        {
            if (shieldPattern == null || HasShield) return null;
            
            float hpRatio = (float)CurrentHP / MaxHP;
            
            foreach (var trigger in shieldPattern.Triggers)
            {
                if (trigger.HasBeenUsed) continue;
                
                if (hpRatio <= trigger.HPThreshold)
                {
                    return trigger;
                }
            }
            
            return null;
        }
        
        public void ActivateShield(ShieldTrigger trigger = null)
        {
            if (HasShield) return;
            
            trigger = trigger ?? GetDefaultTrigger();
            if (trigger == null) return;
            
            int hpToConvert = CalculateHPToConvert(trigger);
            if (hpToConvert <= 0) return;
            
            CurrentHP -= hpToConvert;
            shieldHP = hpToConvert;
            shieldStacks = trigger.StackCount;
            maxShieldStacks = trigger.StackCount;
            
            trigger.HasBeenUsed = true;
            UpdateDefenseWithShield();
        }

        private ShieldTrigger GetDefaultTrigger()
        {
            return shieldPattern != null && shieldPattern.Triggers.Length > 0 
                ? shieldPattern.Triggers[0] 
                : null;
        }

        private int CalculateHPToConvert(ShieldTrigger trigger)
        {
            int hpToConvert = Mathf.RoundToInt(CurrentHP * trigger.HPConversionRatio);
            int minimumHP = Mathf.RoundToInt(MaxHP * shieldPattern.MinimumHPRatio);
            
            return Mathf.Min(hpToConvert, CurrentHP - minimumHP);
        }
        
        public override void TakeDamage(ElementType moveType, int damage)
        {
            int currentTurn = 0;
            if (battleManager != null)
            {
                currentTurn = battleManager.GetCurrentTurn();
            }
            lastDamageTurn = currentTurn;
            
            if (!HasShield)
            {
                base.TakeDamage(moveType, damage);
                return;
            }
            
            float weaknessFactor = GetWeaknessFactor(moveType);
            
            if (weaknessFactor > 1f)
            {
                HandleWeaknessDamage();
            }
            else
            {
                HandleNormalDamage(damage);
            }
        }

        private void HandleWeaknessDamage()
        {
            shieldStacks--;
                
            if (shieldStacks <= 0)
            {
                DestroyShield();
                return;
            }
                
            shieldHP = Mathf.RoundToInt(shieldHP * (shieldStacks / (float)maxShieldStacks));
        }

        private void HandleNormalDamage(int damage)
        {
            int shieldDamage = Mathf.RoundToInt(damage * GetDefenseFactor() * SHIELD_DAMAGE_REDUCTION);
            shieldHP -= shieldDamage;
                
            if (shieldHP <= 0)
            {
                shieldStacks--;
                    
                if (shieldStacks <= 0)
                {
                    DestroyShield();
                }
                else
                {
                    RecalculateShieldHP();
                }
            }
        }

        private void RecalculateShieldHP()
        {
            int originalShieldHP = Mathf.RoundToInt((CurrentHP + shieldHP) * 0.25f);
            shieldHP = Mathf.RoundToInt(originalShieldHP * (shieldStacks / (float)maxShieldStacks));
        }
        
        private void DestroyShield()
        {
            shieldHP = 0;
            shieldStacks = 0;
            UpdateDefenseWithShield();
            ApplyStun(SHIELD_BREAK_STUN_DURATION);
        }
        
        private void UpdateDefenseWithShield()
        {
            float multiplier = HasShield && shieldPattern != null 
                ? shieldPattern.DefenseMultiplier 
                : 1f;
            DefBuffMultiplier = multiplier;
        }
        
        protected override float GetWeaknessFactor(ElementType moveType) 
            => TypeChart.GetEffectiveness(moveType, this.ElementType);

        public override IEnumerator PlayDamageFlash(ElementType attackType, float duration)
        {
            if (SpriteRenderer == null) yield break;

            Color originalColor = SpriteRenderer.color;

            float effectiveness = TypeChart.GetEffectiveness(attackType, this.ElementType);
            bool isWeakness = effectiveness > 1f;

            Color flashColor = isWeakness ? new Color(1f, 0.3f, 0.3f, originalColor.a) : originalColor;

            float flashInterval = 0.15f;
            int flashCount = Mathf.FloorToInt(duration / (flashInterval * 2));

            for (int i = 0; i < flashCount; i++)
            {
                SpriteRenderer.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
                yield return new WaitForSeconds(flashInterval);
        
                SpriteRenderer.color = flashColor;
                yield return new WaitForSeconds(flashInterval);
            }

            SpriteRenderer.color = originalColor;
        }
    }
}