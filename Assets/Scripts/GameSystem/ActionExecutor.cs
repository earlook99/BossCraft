using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using Entity;
using Data;
using GameSystem.Events;
using GameSystem.Utils;

namespace GameSystem
{
    public class ActionExecutor : MonoBehaviour
    {
        [SerializeField] private GameObject _defaultEffectPrefab;
        
        private BattleEntity[] _entities;
        private readonly List<int> _validTargetIndices = new List<int>(5);
        
        private const float MESSAGE_DURATION = 1f;
        private const float EFFECT_DURATION = 2f;
        private const float MULTI_HIT_DELAY = 0.5f;
        private const int MIN_MULTI_HITS = 2;
        private const int MAX_MULTI_HITS = 5;
        
        public void Initialize(BattleEntity[] entities)
        {
            _entities = entities;
        }
        
        public async Task ExecuteActionAsync(ActionData action, CancellationToken ct)
        {
            var source = _entities[(int)action.Source];
            
            if (source.IsGuarding)
            {
                source.SetGuardState(false);
            }
            
            BattleEvents.RaiseActionExecuted(source, action);
            
            switch (action.Action)
            {
                case ActionType.Move:
                    await ExecuteMoveAsync(action, ct);
                    break;
                case ActionType.Guard:
                    await ExecuteGuardAsync(source, ct);
                    break;
                case ActionType.Taunt:
                    await ExecuteTauntAsync(source, ct);
                    break;
            }
        }
        
        private async Task ExecuteMoveAsync(ActionData action, CancellationToken ct)
        {
            var source = _entities[(int)action.Source];
            var moveInst = source.GetMoveInstance(action.ActionIndex);
            if (moveInst == null) return;
            
            if (moveInst.Data.RequiresCharge && !source.IsCharging)
            {
                source.SetChargingState(true, action.ActionIndex, action.Target);
                UIEvents.RaiseShowMessage($"{source.EntityName} is charging up!", MESSAGE_DURATION);
                await AsyncUtilities.WaitForSecondsAsync(MESSAGE_DURATION, ct);
                return;
            }
            
            UIEvents.RaiseShowMessage($"{source.EntityName} used {moveInst.Data.Name}!", MESSAGE_DURATION);
            await AsyncUtilities.WaitForSecondsAsync(MESSAGE_DURATION, ct);
            
            await ApplyMoveEffectsAsync(source, action.Target, moveInst, ct);
            
            if (source.IsCharging)
            {
                source.SetChargingState(false);
            }
            
            moveInst.CooldownLeft = moveInst.Data.Cooldown;
        }
        
        private async Task ExecuteGuardAsync(BattleEntity source, CancellationToken ct)
        {
            source.SetGuardState(true);
            UIEvents.RaiseShowMessage($"{source.EntityName} takes a defensive stance!", MESSAGE_DURATION);
            await AsyncUtilities.WaitForSecondsAsync(MESSAGE_DURATION, ct);
        }
        
        private async Task ExecuteTauntAsync(BattleEntity source, CancellationToken ct)
        {
            UIEvents.RaiseShowMessage($"{source.EntityName} taunts the enemy!", MESSAGE_DURATION);
            await AsyncUtilities.WaitForSecondsAsync(MESSAGE_DURATION, ct);
        }
        
        private async Task ApplyMoveEffectsAsync(BattleEntity source, EntityType targetType, MoveInstance moveInst, CancellationToken ct)
        {
            // VFX를 먼저 한 번만 재생
            var primaryTarget = _entities[(int)targetType];
            await SpawnMoveEffectAsync(primaryTarget, moveInst.Data, ct);
    
            // 그 다음 각 효과 적용
            switch (moveInst.Data.Category)
            {
                case MoveCategory.Single:
                    await ApplyToSingleTargetAsync(source, primaryTarget, moveInst, ct);
                    break;
                // ... 나머지
            }
        }
        
        private async Task SpawnMoveEffectAsync(BattleEntity target, MoveData moveData, CancellationToken ct)
        {
            var effectPoolManager = Pooling.EffectPoolManager.Instance;
            if (effectPoolManager != null && moveData.VFXPrefab != null)
            {
                var effect = effectPoolManager.GetEffect(moveData.VFXPrefab);
                if (effect != null)
                {
                    effect.transform.position = target.transform.position;
                    await AsyncUtilities.WaitForSecondsAsync(EFFECT_DURATION, ct);
                    effectPoolManager.ReturnEffect(effect);
                }
            }
        }
        
        private void GetAOETargets(BattleEntity source, List<int> targetIndices)
        {
            targetIndices.Clear();
            
            if (source is BossEntity)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (_entities[i] != null && _entities[i].CurrentHP > 0)
                    {
                        targetIndices.Add(i);
                    }
                }
            }
            else
            {
                if (_entities[4] != null && _entities[4].CurrentHP > 0)
                {
                    targetIndices.Add(4);
                }
            }
        }
        
        private async Task ApplyToSingleTargetAsync(BattleEntity source, BattleEntity target, MoveInstance moveInst, CancellationToken ct)
        {
            if (target.CurrentHP <= 0) return;
            
            foreach (var effect in moveInst.Data.Effects)
            {
                await ApplyEffectAsync(source, target, moveInst, effect, ct);
            }
        }
        
        private async Task ApplyToMultipleTargetsAsync(BattleEntity source, List<int> targetIndices, MoveInstance moveInst, CancellationToken ct)
        {
            foreach (int idx in targetIndices)
            {
                var target = _entities[idx];
                if (target.CurrentHP <= 0) continue;
                
                foreach (var effect in moveInst.Data.Effects)
                {
                    await ApplyEffectAsync(source, target, moveInst, effect, ct);
                }
            }
        }
        
        private async Task ApplyToRandomTargetsAsync(BattleEntity source, MoveInstance moveInst, CancellationToken ct)
        {
            int hitCount = Random.Range(MIN_MULTI_HITS, MAX_MULTI_HITS);
            
            for (int i = 0; i < hitCount; i++)
            {
                GetValidRandomTargets(source, _validTargetIndices);
                if (_validTargetIndices.Count == 0) break;
                
                int randomIndex = Random.Range(0, _validTargetIndices.Count);
                var target = _entities[_validTargetIndices[randomIndex]];
                
                foreach (var effect in moveInst.Data.Effects)
                {
                    if (effect.EffectType == MoveEffectType.Damage)
                    {
                        await ApplyEffectAsync(source, target, moveInst, effect, ct);
                    }
                }
                
                await AsyncUtilities.WaitForSecondsAsync(MULTI_HIT_DELAY, ct);
            }
        }
        
        private void GetValidRandomTargets(BattleEntity source, List<int> targetIndices)
        {
            targetIndices.Clear();
            
            if (source is BossEntity)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (_entities[i] != null && _entities[i].CurrentHP > 0)
                    {
                        targetIndices.Add(i);
                    }
                }
            }
            else
            {
                if (_entities[4] != null && _entities[4].CurrentHP > 0)
                {
                    targetIndices.Add(4);
                }
            }
        }
        
        private async Task ApplyEffectAsync(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect, CancellationToken ct)
        {
            var processor = GetComponent<BattleEffectProcessor>();
            if (processor == null)
            {
                processor = gameObject.AddComponent<BattleEffectProcessor>();
                processor.Initialize(_entities, _defaultEffectPrefab);
            }
            
            await processor.ProcessEffectAsync(source, target, moveInst, effect, ct);
        }
    }
}