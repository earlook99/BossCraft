using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Entity;
using Data;
using Effect;
using GameSystem.Events;
using GameSystem.Utils;

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
        
        public async Task ProcessEffectAsync(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect, CancellationToken ct)
        {
            switch (effect.EffectType)
            {
                case MoveEffectType.Damage:
                    await ProcessDamageAsync(source, target, moveInst, effect, ct);
                    break;
                case MoveEffectType.Heal:
                    await ProcessHealAsync(source, target, moveInst, effect, ct);
                    break;
                case MoveEffectType.Buff:
                    await ProcessBuffAsync(source, target, moveInst, effect, ct);
                    break;
                case MoveEffectType.Debuff:
                    await ProcessDebuffAsync(source, target, moveInst, effect, ct);
                    break;
                case MoveEffectType.Shield:
                    await ProcessShieldAsync(source, target, moveInst, effect, ct);
                    break;
                case MoveEffectType.Stun:
                    await ProcessStunAsync(source, target, moveInst, effect, ct);
                    break;
            }
        }
        
        private async Task ProcessDamageAsync(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect, CancellationToken ct)
        {
            var hit = DamageFormula.GetRawHit(source, effect.Power, effect.Accuracy, effect.CritChance);
            
            await SpawnEffectAsync(target, moveInst.Data, ct);
            
            if (!hit.IsHit)
            {
                UIEvents.RaiseShowMessage("Miss!", 1f);
                return;
            }
            
            int prevHP = target.CurrentHP;
            target.TakeDamage(moveInst.Data.Type, hit.Damage);
            
            BattleEvents.RaiseDamageDealt(source, target, hit.Damage, moveInst.Data.Type);
            
            await AnimateHPChangeAsync(target, prevHP, target.CurrentHP, ct);
            
            if (target.CurrentHP <= 0)
            {
                BattleEvents.RaiseEntityDefeated(target);
            }
        }
        
        private async Task ProcessHealAsync(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect, CancellationToken ct)
        {
            await SpawnEffectAsync(target, moveInst.Data, ct);
            
            int prevHP = target.CurrentHP;
            target.CurrentHP = Mathf.Min(target.CurrentHP + effect.Power, target.MaxHP);
            
            BattleEvents.RaiseHealingReceived(source, target, effect.Power);
            
            await AnimateHPChangeAsync(target, prevHP, target.CurrentHP, ct);
        }
        
        private async Task ProcessBuffAsync(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect, CancellationToken ct)
        {
            ApplyStatModifier(target, effect, true);
            BattleEvents.RaiseStatusEffectApplied(source, target, effect.EffectType);
            await SpawnEffectAsync(target, moveInst.Data, ct);
        }
        
        private async Task ProcessDebuffAsync(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect, CancellationToken ct)
        {
            ApplyStatModifier(target, effect, false);
            BattleEvents.RaiseStatusEffectApplied(source, target, effect.EffectType);
            await SpawnEffectAsync(target, moveInst.Data, ct);
        }
        
        private async Task ProcessShieldAsync(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect, CancellationToken ct)
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
                    
                    await AsyncUtilities.WaitForSecondsAsync(0.5f, ct);
                    await SpawnEffectAsync(target, moveInst.Data, ct);
                }
            }
        }
        
        private async Task ProcessStunAsync(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect, CancellationToken ct)
        {
            if (DamageFormula.CheckStun(effect.Accuracy))
            {
                target.ApplyStun(SHIELD_BREAK_STUN_DURATION);
                BattleEvents.RaiseStatusEffectApplied(source, target, effect.EffectType);
                await SpawnEffectAsync(target, moveInst.Data, ct);
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
        
        private async Task SpawnEffectAsync(BattleEntity target, MoveData moveData, CancellationToken ct)
        {
            var effectPoolManager = Pooling.EffectPoolManager.Instance;
            if (effectPoolManager != null && moveData.VFXPrefab != null)
            {
                string poolName = moveData.VFXPrefab.name;
                var effect = effectPoolManager.GetEffect(poolName, target.transform.position, Quaternion.identity);
                if (effect != null)
                {
                    await AsyncUtilities.WaitForSecondsAsync(EFFECT_DURATION, ct);
                    effectPoolManager.ReturnEffect(effect);
                }
            }
            else if (_defaultEffectPrefab != null && effectPoolManager != null)
            {
                var effect = effectPoolManager.GetEffect("DefaultEffect", target.transform.position, Quaternion.identity);
                if (effect != null)
                {
                    await AsyncUtilities.WaitForSecondsAsync(EFFECT_DURATION, ct);
                    effectPoolManager.ReturnEffect(effect);
                }
            }
        }
        
        private async Task AnimateHPChangeAsync(BattleEntity target, int startHP, int endHP, CancellationToken ct)
        {
            int entityIndex = Array.IndexOf(_entities, target);
            float elapsed = 0f;
            
            while (elapsed < HP_ANIMATION_DURATION && !ct.IsCancellationRequested)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / HP_ANIMATION_DURATION;
                int currentHP = (int)Mathf.Lerp(startHP, endHP, t);
                
                UIEvents.RaiseUpdateHPBar(entityIndex, currentHP, target.MaxHP);
                
                await AsyncUtilities.NextFrameAsync(ct);
            }
            
            if (!ct.IsCancellationRequested)
            {
                UIEvents.RaiseUpdateHPBar(entityIndex, endHP, target.MaxHP);
            }
        }
    }
}