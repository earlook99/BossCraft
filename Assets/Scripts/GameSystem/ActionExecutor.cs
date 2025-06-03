using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Entity;
using Data;
using GameSystem.Events;
using GameSystem.Utils;
using System.Linq;

namespace GameSystem
{
    public class ActionExecutor : MonoBehaviour
    {
        [SerializeField] private GameObject _defaultEffectPrefab;
        
        private BattleEntity[] _entities;
        
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
            switch (moveInst.Data.Category)
            {
                case MoveCategory.Single:
                    await ApplyToSingleTargetAsync(source, _entities[(int)targetType], moveInst, ct);
                    break;
                case MoveCategory.AOE:
                    await ApplyToMultipleTargetsAsync(source, GetAOETargets(source), moveInst, ct);
                    break;
                case MoveCategory.MultiRandom:
                    await ApplyToRandomTargetsAsync(source, moveInst, ct);
                    break;
            }
        }
        
        private int[] GetAOETargets(BattleEntity source)
        {
            if (source is BossEntity)
            {
                return Enumerable.Range(0, 4).Where(i => _entities[i].CurrentHP > 0).ToArray();
            }
            else
            {
                return new[] { 4 };
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
        
        private async Task ApplyToMultipleTargetsAsync(BattleEntity source, int[] targetIndices, MoveInstance moveInst, CancellationToken ct)
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
                var validTargets = GetValidRandomTargets(source);
                if (validTargets.Length == 0) break;
                
                var target = _entities[validTargets[Random.Range(0, validTargets.Length)]];
                
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
        
        private int[] GetValidRandomTargets(BattleEntity source)
        {
            if (source is BossEntity)
            {
                return Enumerable.Range(0, 4).Where(i => _entities[i].CurrentHP > 0).ToArray();
            }
            else
            {
                return new[] { 4 }.Where(i => _entities[i].CurrentHP > 0).ToArray();
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