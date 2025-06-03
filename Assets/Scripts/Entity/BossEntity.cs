using System;
using System.Collections;
using AI;
using GameSystem;
using UnityEngine;
using Data;

namespace Entity
{
    public class BossEntity : BattleEntity
    {
        [Header("Shield System")]
        [SerializeField] private ShieldPattern _shieldPattern;
        
        private int _shieldHP = 0;
        private int _shieldStacks = 0;
        private int _maxShieldStacks = 3;
        private int _lastDamageTurn = -1;
        
        private const float SHIELD_DAMAGE_REDUCTION = 0.5f;
        private const int SHIELD_BREAK_STUN_DURATION = 1;

        public bool HasShield => _shieldHP > 0;
        public int ShieldHP => _shieldHP;
        public int ShieldStacks => _shieldStacks;
        public int MaxShieldStacks => _maxShieldStacks;
        public ShieldPattern ShieldPattern => _shieldPattern;

        protected override void Start()
        {
            base.Start();
    
            if (_shieldPattern != null)
            {
                _shieldPattern.ResetTriggers();
            }
        }
        
        public ShieldTrigger GetAvailableShieldTrigger(int currentTurn)
        {
            if (_shieldPattern == null || HasShield) return null;
            
            float hpRatio = (float)CurrentHP / MaxHP;
            
            foreach (var trigger in _shieldPattern.Triggers)
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
            _shieldHP = hpToConvert;
            _shieldStacks = trigger.StackCount;
            _maxShieldStacks = trigger.StackCount;
            
            trigger.HasBeenUsed = true;
            UpdateDefenseWithShield();
        }

        private ShieldTrigger GetDefaultTrigger()
        {
            return _shieldPattern != null && _shieldPattern.Triggers.Length > 0 
                ? _shieldPattern.Triggers[0] 
                : null;
        }

        private int CalculateHPToConvert(ShieldTrigger trigger)
        {
            int hpToConvert = Mathf.RoundToInt(CurrentHP * trigger.HPConversionRatio);
            int minimumHP = Mathf.RoundToInt(MaxHP * _shieldPattern.MinimumHPRatio);
            
            return Mathf.Min(hpToConvert, CurrentHP - minimumHP);
        }
        
        public override void TakeDamage(ElementType moveType, int damage)
        {
            int currentTurn = 0;
            var battleContext = BattleContext.Instance;
            if (battleContext != null)
            {
                currentTurn = battleContext.GetCurrentTurn();
            }
            _lastDamageTurn = currentTurn;
            
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
            _shieldStacks--;
                
            if (_shieldStacks <= 0)
            {
                DestroyShield();
                return;
            }
                
            _shieldHP = Mathf.RoundToInt(_shieldHP * (_shieldStacks / (float)_maxShieldStacks));
        }

        private void HandleNormalDamage(int damage)
        {
            int shieldDamage = Mathf.RoundToInt(damage * GetDefenseFactor() * SHIELD_DAMAGE_REDUCTION);
            _shieldHP -= shieldDamage;
                
            if (_shieldHP <= 0)
            {
                _shieldStacks--;
                    
                if (_shieldStacks <= 0)
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
            int originalShieldHP = Mathf.RoundToInt((CurrentHP + _shieldHP) * 0.25f);
            _shieldHP = Mathf.RoundToInt(originalShieldHP * (_shieldStacks / (float)_maxShieldStacks));
        }
        
        private void DestroyShield()
        {
            _shieldHP = 0;
            _shieldStacks = 0;
            UpdateDefenseWithShield();
            ApplyStun(SHIELD_BREAK_STUN_DURATION);
        }
        
        private void UpdateDefenseWithShield()
        {
            float multiplier = HasShield && _shieldPattern != null 
                ? _shieldPattern.DefenseMultiplier 
                : 1f;
            DefBuffMultiplier = multiplier;
        }
        
        protected override float GetWeaknessFactor(ElementType moveType) 
            => TypeChart.GetEffectiveness(moveType, this.ElementType);
    }
}