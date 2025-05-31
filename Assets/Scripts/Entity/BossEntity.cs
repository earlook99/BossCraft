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
            
            if (trigger == null && _shieldPattern != null && _shieldPattern.Triggers.Length > 0)
            {
                trigger = _shieldPattern.Triggers[0];
            }
            
            if (trigger == null) return;
            
            int hpToConvert = Mathf.RoundToInt(CurrentHP * trigger.HPConversionRatio);
            int minimumHP = Mathf.RoundToInt(MaxHP * _shieldPattern.MinimumHPRatio);
            
            hpToConvert = Mathf.Min(hpToConvert, CurrentHP - minimumHP);
            
            if (hpToConvert <= 0) return;
            
            CurrentHP -= hpToConvert;
            _shieldHP = hpToConvert;
            _shieldStacks = trigger.StackCount;
            _maxShieldStacks = trigger.StackCount;
            
            trigger.HasBeenUsed = true;
            
            UpdateDefenseWithShield();
        }
        
        public override void TakeDamage(ElementType moveType, int damage)
        {
            _lastDamageTurn = FindObjectOfType<BattleManager>()?.TurnCount ?? 0;
            
            if (!HasShield)
            {
                base.TakeDamage(moveType, damage);
                return;
            }
            
            float weaknessFactor = GetWeaknessFactor(moveType);
            
            if (weaknessFactor > 1f)
            {
                _shieldStacks--;
                
                if (_shieldStacks <= 0)
                {
                    DestroyShield();
                    return;
                }
                
                _shieldHP = Mathf.RoundToInt(_shieldHP * (_shieldStacks / (float)_maxShieldStacks));
            }
            else
            {
                int shieldDamage = Mathf.RoundToInt(damage * GetDefenseFactor() * 0.5f);
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
                        int originalShieldHP = Mathf.RoundToInt((CurrentHP + _shieldHP) * 0.25f);
                        _shieldHP = Mathf.RoundToInt(originalShieldHP * (_shieldStacks / (float)_maxShieldStacks));
                    }
                }
            }
        }
        
        private void DestroyShield()
        {
            _shieldHP = 0;
            _shieldStacks = 0;
            UpdateDefenseWithShield();
            ApplyStun(1);
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