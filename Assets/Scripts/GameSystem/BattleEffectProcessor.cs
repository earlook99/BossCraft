using System;
using System.Collections;
using UnityEngine;
using Entity;
using Data;
using Effect;
using GameSystem.UI;

namespace GameSystem
{
    public class BattleEffectProcessor : MonoBehaviour
    {
        private BattleEntity[] _entities;
        private GameObject _defaultEffectPrefab;
        
        private BattleUIController _battleUIController;
        private StatusUIManager _statusUIManager;
        private MessageUIManager _messageUIManager;
        private BattleManager _battleManager;
        
        private const float EFFECT_DURATION = 2f;
        private const float HP_ANIMATION_DURATION = 1f;
        private const float CRITICAL_MULTIPLIER = 1.5f;
        private const int MINIMUM_DAMAGE = 1;
        private const float MIN_MULTIPLIER = 0.6f;
        private const float MAX_MULTIPLIER = 1.4f;
        private const int SHIELD_BREAK_STUN_DURATION = 1;
        
        public void Initialize(BattleEntity[] entities, GameObject defaultEffectPrefab)
        {
            _entities = entities;
            _defaultEffectPrefab = defaultEffectPrefab;
            
            if (_battleManager == null)
                _battleManager = GetComponent<BattleManager>();
        }
        
        public void SetUIReferences(BattleUIController battleUI, StatusUIManager statusUI, MessageUIManager messageUI)
        {
            _battleUIController = battleUI;
            _statusUIManager = statusUI;
            _messageUIManager = messageUI;
        }
        
        public void OnDamageDealt(BattleEntity source, BattleEntity target, int damage, ElementType element)
        {
            if (_statusUIManager != null)
                _statusUIManager.OnDamageDealt(target);
        }
        
        public IEnumerator ProcessEffect(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect)
        {
            switch (effect.EffectType)
            {
                case MoveEffectType.Damage:
                    yield return ProcessDamage(source, target, moveInst, effect);
                    break;
                case MoveEffectType.Heal:
                    yield return ProcessHeal(source, target, moveInst, effect);
                    break;
                case MoveEffectType.Buff:
                    yield return ProcessBuff(source, target, moveInst, effect);
                    break;
                case MoveEffectType.Debuff:
                    yield return ProcessDebuff(source, target, moveInst, effect);
                    break;
                case MoveEffectType.Shield:
                    yield return ProcessShield(source, target, moveInst, effect);
                    break;
                case MoveEffectType.Stun:
                    yield return ProcessStun(source, target, moveInst, effect);
                    break;
                case MoveEffectType.Stealth:
                    yield return ProcessStealth(source, target, moveInst, effect);
                    break;
                case MoveEffectType.Counter:
                    yield return ProcessCounter(source, target, moveInst, effect);
                    break;
                case MoveEffectType.Taunt:
                    yield return ProcessTaunt(source, target, moveInst, effect);
                    break;
            }
        }
        
        private IEnumerator ProcessDamage(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect)
        {
            var hit = DamageFormula.GetRawHit(source, effect.Power, effect.Accuracy, effect.CritChance);
    
            yield return SpawnEffect(target, moveInst.Data);
    
            if (!hit.IsHit)
            {
                if (_messageUIManager != null)
                    _messageUIManager.ShowMessage("Miss!", 1f);
                yield break;
            }
    
            float effectiveness = TypeChart.GetEffectiveness(moveInst.Data.Type, target.ElementType);
            bool isWeakness = effectiveness > 1f;
    
            int prevHP = target.CurrentHP;
            target.TakeDamage(moveInst.Data.Type, hit.Damage);
    
            if (_statusUIManager != null)
                _statusUIManager.OnDamageDealt(target);
    
            StartCoroutine(AnimateHPChange(target, prevHP, target.CurrentHP));
            yield return target.PlayDamageFlash(moveInst.Data.Type, HP_ANIMATION_DURATION);
    
            if (isWeakness && target is BossEntity)
            {
                if (_messageUIManager != null)
                    _messageUIManager.ShowMessage("효과가 굉장했다!", 2f);
                yield return new WaitForSeconds(2f);
            }
    
            if (target.CurrentHP <= 0)
            {
                if (_battleManager != null)
                    _battleManager.OnEntityDefeated(target);
            }
        }
        
        private IEnumerator ProcessHeal(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect)
        {
            yield return SpawnEffect(target, moveInst.Data);
            
            int prevHP = target.CurrentHP;
            target.CurrentHP = Mathf.Min(target.CurrentHP + effect.Power, target.MaxHP);
            
            if (_statusUIManager != null)
                _statusUIManager.OnHealingReceived(target);
            
            yield return AnimateHPChange(target, prevHP, target.CurrentHP);
        }
        
        private IEnumerator ProcessBuff(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect)
        {
            ApplyStatModifier(target, effect, true);
            
            if (_statusUIManager != null && effect.BuffsType != BuffsType.None)
            {
                int stackCount = effect.BuffsType == BuffsType.Attack ? 
                    target.AttackBuffStacks : target.DefenseBuffStacks;
                _statusUIManager.OnBuffStackChanged(target, effect.BuffsType, stackCount);
            }
            
            yield return SpawnEffect(target, moveInst.Data);
        }
        
        private IEnumerator ProcessDebuff(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect)
        {
            ApplyStatModifier(target, effect, false);
            
            if (_statusUIManager != null && effect.BuffsType != BuffsType.None)
            {
                int stackCount = effect.BuffsType == BuffsType.Attack ? 
                    target.AttackBuffStacks : target.DefenseBuffStacks;
                _statusUIManager.OnBuffStackChanged(target, effect.BuffsType, stackCount);
            }
            
            yield return SpawnEffect(target, moveInst.Data);
        }
        
        private IEnumerator ProcessShield(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect)
        {
            if (target is BossEntity boss)
            {
                var turnManager = GetComponent<TurnManager>();
                int currentTurn = turnManager != null ? turnManager.TurnCount : 0;
                var trigger = boss.GetAvailableShieldTrigger(currentTurn);
                
                if (trigger != null)
                {
                    int previousHP = boss.CurrentHP;
                    boss.ActivateShield(trigger);
                    
                    if (_statusUIManager != null)
                        _statusUIManager.OnShieldActivated(boss, boss.ShieldHP);
                        
                    if (_messageUIManager != null)
                        _messageUIManager.ShowMessage($"{boss.EntityName} activates shield!", 1.5f);
                    
                    yield return new WaitForSeconds(0.5f);
                    yield return SpawnEffect(target, moveInst.Data);
                }
            }
        }
        
        private IEnumerator ProcessStun(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect)
        {
            if (DamageFormula.CheckStun(effect.Accuracy))
            {
                target.ApplyStun(SHIELD_BREAK_STUN_DURATION);
                yield return SpawnEffect(target, moveInst.Data);
            }
        }
        
        private IEnumerator ProcessStealth(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect)
        {
            target.ApplyStealth(2);
            
            if (_messageUIManager != null)
                _messageUIManager.ShowMessage($"{target.EntityName} becomes stealthed!", 1.5f);
                
            yield return new WaitForSeconds(1.5f);
        }
        
        private IEnumerator ProcessCounter(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect)
        {
            target.ApplyStatusEffect(MoveEffectType.Counter, 3);
            
            if (_messageUIManager != null)
                _messageUIManager.ShowMessage($"{target.EntityName} prepares to counter!", 1.5f);
                
            yield return new WaitForSeconds(1.5f);
        }

        private IEnumerator ProcessTaunt(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect)
        {
            target.ApplyStatusEffect(MoveEffectType.Taunt, 2);
            
            if (_messageUIManager != null)
                _messageUIManager.ShowMessage($"{target.EntityName} taunts the enemy!", 1.5f);
                
            yield return new WaitForSeconds(1.5f);
        }
        
        private void ApplyStatModifier(BattleEntity target, MoveEffect effect, bool isBuff)
        {
            float modifier = effect.Power / 100f;
            if (!isBuff) modifier = -modifier;
            
            switch (effect.BuffsType)
            {
                case BuffsType.Attack:
                    target.AtkBuffMultiplier = Mathf.Clamp(
                        target.AtkBuffMultiplier + modifier,
                        MIN_MULTIPLIER,
                        MAX_MULTIPLIER
                    );
                    break;
                case BuffsType.Defense:
                    target.DefBuffMultiplier = Mathf.Clamp(
                        target.DefBuffMultiplier + Mathf.Abs(modifier),
                        MIN_MULTIPLIER,
                        MAX_MULTIPLIER
                    );
                    break;
            }
        }
        
        private IEnumerator SpawnEffect(BattleEntity target, MoveData moveData)
        {
            var effectPoolManager = Pooling.EffectPoolManager.Instance;
            if (effectPoolManager != null && moveData.VFXPrefab != null)
            {
                var effect = effectPoolManager.GetEffect(moveData.VFXPrefab);
                if (effect != null)
                {
                    effect.transform.position = target.transform.position;
                    yield return new WaitForSeconds(EFFECT_DURATION);
                    effectPoolManager.ReturnEffect(effect);
                }
            }
        }
        
        private IEnumerator AnimateHPChange(BattleEntity target, int startHP, int endHP)
        {
            int entityIndex = Array.IndexOf(_entities, target);
            float elapsed = 0f;
            
            while (elapsed < HP_ANIMATION_DURATION)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / HP_ANIMATION_DURATION;
                int currentHP = (int)Mathf.Lerp(startHP, endHP, t);
                
                if (_statusUIManager != null)
                    _statusUIManager.UpdateHPBar(entityIndex, currentHP, target.MaxHP);
                
                yield return null;
            }
            
            if (_statusUIManager != null)
                _statusUIManager.UpdateHPBar(entityIndex, endHP, target.MaxHP);
        }
    }
}