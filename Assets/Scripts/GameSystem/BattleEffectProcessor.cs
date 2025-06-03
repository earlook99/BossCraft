using System;
using System.Collections;
using UnityEngine;
using Entity;
using Data;
using Effect;
using GameSystem.Events;

namespace GameSystem
{
    public class BattleEffectProcessor : MonoBehaviour
    {
        private BattleEntity[] _entities;
        private GameObject _defaultEffectPrefab;
        
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
            }
        }
        
        private IEnumerator ProcessDamage(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect)
        {
            var hit = DamageFormula.GetRawHit(source, effect.Power, effect.Accuracy, effect.CritChance);
            
            yield return SpawnEffect(target, moveInst.Data);
            
            if (!hit.IsHit)
            {
                UIEvents.RaiseShowMessage("Miss!", 1f);
                yield break;
            }
            
            int prevHP = target.CurrentHP;
            target.TakeDamage(moveInst.Data.Type, hit.Damage);
            
            BattleEvents.RaiseDamageDealt(source, target, hit.Damage, moveInst.Data.Type);
            
            yield return AnimateHPChange(target, prevHP, target.CurrentHP);
            
            if (target.CurrentHP <= 0)
            {
                BattleEvents.RaiseEntityDefeated(target);
            }
        }
        
        private IEnumerator ProcessHeal(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect)
        {
            yield return SpawnEffect(target, moveInst.Data);
            
            int prevHP = target.CurrentHP;
            target.CurrentHP = Mathf.Min(target.CurrentHP + effect.Power, target.MaxHP);
            
            BattleEvents.RaiseHealingReceived(source, target, effect.Power);
            
            yield return AnimateHPChange(target, prevHP, target.CurrentHP);
        }
        
        private IEnumerator ProcessBuff(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect)
        {
            ApplyStatModifier(target, effect, true);
            BattleEvents.RaiseStatusEffectApplied(source, target, effect.EffectType);
            yield return SpawnEffect(target, moveInst.Data);
        }
        
        private IEnumerator ProcessDebuff(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect)
        {
            ApplyStatModifier(target, effect, false);
            BattleEvents.RaiseStatusEffectApplied(source, target, effect.EffectType);
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
                    
                    BattleEvents.RaiseShieldActivated(boss, boss.ShieldHP);
                    UIEvents.RaiseShowMessage($"{boss.EntityName} activates shield!", 1.5f);
                    
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
                BattleEvents.RaiseStatusEffectApplied(source, target, effect.EffectType);
                yield return SpawnEffect(target, moveInst.Data);
            }
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
                string poolName = moveData.VFXPrefab.name;
                yield return effectPoolManager.PlayEffectForDuration(poolName, target.transform.position, EFFECT_DURATION);
            }
            else if (_defaultEffectPrefab != null)
            {
                yield return Pooling.EffectPoolManager.Instance?.PlayEffectForDuration("DefaultEffect", target.transform.position, EFFECT_DURATION);
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
                
                UIEvents.RaiseUpdateHPBar(entityIndex, currentHP, target.MaxHP);
                
                yield return null;
            }
            
            UIEvents.RaiseUpdateHPBar(entityIndex, endHP, target.MaxHP);
        }
    }
}