using System.Collections;
using GameSystem;
using UnityEngine;
using Data;
using static GameSystem.GameConstants.Battle;

namespace Entity
{
    public class BossEntity : BattleEntity
    {
        [Header("Shield System")]
        [SerializeField] private ShieldPattern shieldPattern;
        
        private int shieldHP = 0;
        private int shieldStacks = 0;
        private int maxShieldStacks = 3;
        private int maxShieldHP = 0; // 실드 활성화 시 최대 HP
        private int lastDamageTurn = -1;
        private bool[] usedShieldTriggers;
        
        public bool HasShield => shieldHP > 0;
        public int ShieldHP => shieldHP;
        public int ShieldStacks => shieldStacks;
        public int MaxShieldStacks => maxShieldStacks;
        public int MaxShieldHP => maxShieldHP;
        public ShieldPattern ShieldPattern => shieldPattern;

        protected override IEnumerator Start()
        {
            yield return base.Start();
            
            if (shieldPattern != null)
            {
                shieldPattern.ResetTriggers();
                usedShieldTriggers = new bool[shieldPattern.Triggers.Length];
            }
        }
        
        public ShieldTrigger GetAvailableShieldTrigger(int currentTurn)
        {
            if (shieldPattern == null || HasShield) 
            {
                Debug.Log($"[SHIELD CHECK] Returning null - HasShield: {HasShield}, ShieldHP: {shieldHP}");
                return null;
            }
            
            float hpRatio = (float)CurrentHP / MaxHP;
            Debug.Log($"[SHIELD CHECK] HP Ratio: {hpRatio:F2}, Current HP: {CurrentHP}, Max HP: {MaxHP}");
            
            for (int i = 0; i < shieldPattern.Triggers.Length; i++)
            {
                if (usedShieldTriggers[i]) 
                {
                    Debug.Log($"[SHIELD CHECK] Trigger {i} already used");
                    continue;
                }
                
                var trigger = shieldPattern.Triggers[i];
                Debug.Log($"[SHIELD CHECK] Checking trigger {i} - Threshold: {trigger.HPThreshold:F2}");
                if (hpRatio <= trigger.HPThreshold)
                {
                    Debug.Log($"[SHIELD CHECK] Trigger {i} available!");
                    return trigger;
                }
            }
            
            Debug.Log("[SHIELD CHECK] No available triggers");
            return null;
        }
        
        public void ActivateShield(ShieldTrigger trigger = null)
        {
            Debug.Log($"[SHIELD ACTIVATE] Called - HasShield: {HasShield}, ShieldHP: {shieldHP}");
            if (HasShield) 
            {
                Debug.LogWarning("[SHIELD ACTIVATE] Already has shield! Aborting.");
                return;
            }
            
            trigger = trigger ?? GetDefaultTrigger();
            if (trigger == null) return;
            
            int hpToConvert = CalculateHPToConvert(trigger);
            if (hpToConvert <= 0) return;
            
            Debug.Log($"[SHIELD ACTIVATE] Converting {hpToConvert} HP to shield");
            CurrentHP -= hpToConvert;
            shieldHP = hpToConvert;
            shieldStacks = trigger.StackCount;
            maxShieldStacks = trigger.StackCount;
            maxShieldHP = hpToConvert; // 최대 실드 HP 저장
            
            // Mark this trigger as used in our local array
            for (int i = 0; i < shieldPattern.Triggers.Length; i++)
            {
                if (shieldPattern.Triggers[i] == trigger)
                {
                    usedShieldTriggers[i] = true;
                    Debug.Log($"[SHIELD ACTIVATE] Marked trigger {i} as used");
                    break;
                }
            }
            
            UpdateDefenseWithShield();
            Debug.Log($"[SHIELD ACTIVATE] Shield activated - ShieldHP: {shieldHP}, Stacks: {shieldStacks}");
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
            
            // 실드 UI 업데이트 요청
            if (battleManager != null)
            {
                battleManager.NotifyShieldUpdate(this);
            }
        }
        
        public bool WasWeaknessHit { get; private set; }
        public bool WasStackBroken { get; private set; }
        public bool WasCompletelyDestroyed { get; private set; }
        public int OldStackCount { get; private set; }
        
        public void ResetDamageFlags()
        {
            WasWeaknessHit = false;
            WasStackBroken = false;
            WasCompletelyDestroyed = false;
        }

        private void HandleWeaknessDamage()
        {
            Debug.Log($"[SHIELD WEAKNESS] Taking weakness damage! Current stacks: {shieldStacks}");
            int oldStacks = shieldStacks;
            shieldStacks--;
            WasWeaknessHit = true;
                
            if (shieldStacks <= 0)
            {
                DestroyShield();
                return;
            }
            
            // 실드 스택이 감소했을 때 실드 HP를 다음 칸의 경계값으로 설정
            // 예: 3칸 중 1칸 파괴 → 남은 2칸의 최대값 (전체의 2/3)
            shieldHP = Mathf.RoundToInt(maxShieldHP * (shieldStacks / (float)maxShieldStacks));
            Debug.Log($"[SHIELD WEAKNESS] Stack removed! Remaining stacks: {shieldStacks}, Shield HP set to next boundary: {shieldHP}");
            
            // 스택 파괴 이벤트는 BattleManager에서 처리 (효과가 굉장했다 이후에)
        }

        private void HandleNormalDamage(int damage)
        {
            int shieldDamage = Mathf.RoundToInt(damage * GetDefenseFactor() * SHIELD_DAMAGE_REDUCTION);
            int oldShieldHP = shieldHP;
            shieldHP -= shieldDamage;
            Debug.Log($"[SHIELD DAMAGE] Took {shieldDamage} damage, Shield HP: {oldShieldHP} -> {shieldHP}/{maxShieldHP}");
            
            // 실드 스택 계산
            float oldHpRatio = (float)oldShieldHP / maxShieldHP;
            float newHpRatio = (float)shieldHP / maxShieldHP;
            int oldStack = Mathf.CeilToInt(oldHpRatio * maxShieldStacks);
            int newStack = Mathf.Max(0, Mathf.CeilToInt(newHpRatio * maxShieldStacks));
            
            // 스택이 감소했는지 체크
            if (newStack < oldStack && newStack > 0)
            {
                // 경계값까지만 데미지 적용 (남은 데미지는 버림)
                shieldHP = Mathf.RoundToInt(maxShieldHP * (newStack / (float)maxShieldStacks));
                shieldStacks = newStack;
                WasStackBroken = true;
                OldStackCount = oldStack;
                Debug.Log($"[SHIELD DAMAGE] Stack broken! {oldStack} -> {newStack}, HP clamped to boundary: {shieldHP}");
            }
            else if (shieldHP <= 0)
            {
                // 실드 완전 파괴
                shieldHP = 0;
                shieldStacks = 0;
                WasStackBroken = true;
                WasCompletelyDestroyed = true;
                OldStackCount = oldStack;
                Debug.Log($"[SHIELD DAMAGE] Shield completely destroyed!");
            }
            else
            {
                // 스택 변화 없이 데미지만 입음
                shieldStacks = newStack;
            }
        }

        private void RecalculateShieldHP()
        {
            // This method is now obsolete - shield HP calculation is done inline in HandleNormalDamage
            Debug.LogWarning("[SHIELD] RecalculateShieldHP called but is obsolete");
        }
        
        private void DestroyShield()
        {
            Debug.Log("[SHIELD] Shield destroyed!");
            shieldHP = 0;
            shieldStacks = 0;
            UpdateDefenseWithShield();
            ApplyStun(SHIELD_BREAK_STUN_DURATION);
            
            // 완전 파괴 플래그 설정
            WasCompletelyDestroyed = true;
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