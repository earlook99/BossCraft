using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Entity;
using Data;
using GameSystem.Events;

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
                    var target = _entities[(int)targetType];
                    yield return ApplyToSingleTarget(source, target, moveInst);
                    break;
                    
                case MoveCategory.AOE:
                    GetAOETargets(source, _validTargetIndices);
                    yield return ApplyToMultipleTargets(source, _validTargetIndices, moveInst);
                    break;
                    
                case MoveCategory.MultiRandom:
                    yield return ApplyToRandomTargets(source, moveInst);
                    break;
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
        
        private IEnumerator ApplyToSingleTarget(BattleEntity source, BattleEntity target, MoveInstance moveInst)
        {
            if (target.CurrentHP <= 0) yield break;
            
            foreach (var effect in moveInst.Data.Effects)
            {
                yield return ApplyEffect(source, target, moveInst, effect);
            }
        }
        
        private IEnumerator ApplyToMultipleTargets(BattleEntity source, List<int> targetIndices, MoveInstance moveInst)
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
                GetValidRandomTargets(source, _validTargetIndices);
                if (_validTargetIndices.Count == 0) break;
                
                int randomIndex = Random.Range(0, _validTargetIndices.Count);
                var target = _entities[_validTargetIndices[randomIndex]];
                
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