using System.Collections;
using UnityEngine;
using Entity;
using Data;
using GameSystem.Events;
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
        
        public IEnumerator ExecuteAction(ActionData action)
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
                    yield return ExecuteMove(action);
                    break;
                case ActionType.Guard:
                    yield return ExecuteGuard(source);
                    break;
                case ActionType.Taunt:
                    yield return ExecuteTaunt(source);
                    break;
            }
        }
        
        private IEnumerator ExecuteMove(ActionData action)
        {
            var source = _entities[(int)action.Source];
            var moveInst = source.GetMoveInstance(action.ActionIndex);
            if (moveInst == null) yield break;
            
            if (moveInst.Data.RequiresCharge && !source.IsCharging)
            {
                source.SetChargingState(true, action.ActionIndex, action.Target);
                UIEvents.RaiseShowMessage($"{source.EntityName} is charging up!", MESSAGE_DURATION);
                yield return new WaitForSeconds(MESSAGE_DURATION);
                yield break;
            }
            
            UIEvents.RaiseShowMessage($"{source.EntityName} used {moveInst.Data.Name}!", MESSAGE_DURATION);
            yield return new WaitForSeconds(MESSAGE_DURATION);
            
            yield return ApplyMoveEffects(source, action.Target, moveInst);
            
            if (source.IsCharging)
            {
                source.SetChargingState(false);
            }
            
            moveInst.CooldownLeft = moveInst.Data.Cooldown;
        }
        
        private IEnumerator ExecuteGuard(BattleEntity source)
        {
            source.SetGuardState(true);
            UIEvents.RaiseShowMessage($"{source.EntityName} takes a defensive stance!", MESSAGE_DURATION);
            yield return new WaitForSeconds(MESSAGE_DURATION);
        }
        
        private IEnumerator ExecuteTaunt(BattleEntity source)
        {
            UIEvents.RaiseShowMessage($"{source.EntityName} taunts the enemy!", MESSAGE_DURATION);
            yield return new WaitForSeconds(MESSAGE_DURATION);
        }
        
        private IEnumerator ApplyMoveEffects(BattleEntity source, EntityType targetType, MoveInstance moveInst)
        {
            switch (moveInst.Data.Category)
            {
                case MoveCategory.Single:
                    yield return ApplyToSingleTarget(source, _entities[(int)targetType], moveInst);
                    break;
                case MoveCategory.AOE:
                    yield return ApplyToMultipleTargets(source, GetAOETargets(source), moveInst);
                    break;
                case MoveCategory.MultiRandom:
                    yield return ApplyToRandomTargets(source, moveInst);
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
        
        private IEnumerator ApplyToSingleTarget(BattleEntity source, BattleEntity target, MoveInstance moveInst)
        {
            if (target.CurrentHP <= 0) yield break;
            
            foreach (var effect in moveInst.Data.Effects)
            {
                yield return ApplyEffect(source, target, moveInst, effect);
            }
        }
        
        private IEnumerator ApplyToMultipleTargets(BattleEntity source, int[] targetIndices, MoveInstance moveInst)
        {
            foreach (int idx in targetIndices)
            {
                var target = _entities[idx];
                if (target.CurrentHP <= 0) continue;
                
                foreach (var effect in moveInst.Data.Effects)
                {
                    yield return ApplyEffect(source, target, moveInst, effect);
                }
            }
        }
        
        private IEnumerator ApplyToRandomTargets(BattleEntity source, MoveInstance moveInst)
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
                        yield return ApplyEffect(source, target, moveInst, effect);
                    }
                }
                
                yield return new WaitForSeconds(MULTI_HIT_DELAY);
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
        
        private IEnumerator ApplyEffect(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect)
        {
            var processor = GetComponent<BattleEffectProcessor>();
            if (processor == null)
            {
                processor = gameObject.AddComponent<BattleEffectProcessor>();
                processor.Initialize(_entities, _defaultEffectPrefab);
            }
            
            yield return processor.ProcessEffect(source, target, moveInst, effect);
        }
    }
}