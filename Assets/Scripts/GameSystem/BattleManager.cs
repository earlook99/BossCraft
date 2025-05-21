using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Data;
using Entity;
using NUnit.Framework;
using UnityEngine;

namespace GameSystem
{
    public enum BattleState
    {
        PlayerChoice,
        PlayerAction,
        BossAction,
        CheckBattleEnd
    }
    public class BattleManager : MonoBehaviour
    {
        [SerializeField] private UIManager _uiManager;
        
        public AIWeights Weights;
        
        private BattleState _currentState;

        [SerializeField] private BattleEntity[] _entities = new BattleEntity[5];
        public BattleEntity[] Entities => _entities;

        private const int PlayerCount = 4; // 4 players
        
        private int _turnCount = 0;
        public int TurnCount => _turnCount;

        private int _currentPlayerIndex;
        
        public event Action OnEntitiesInitialized;

        [SerializeField] private GameObject _testEffect;

        private void Start()
        {
            _currentPlayerIndex = 0;
            
            OnEntitiesInitialized?.Invoke();
            
            SetState(BattleState.PlayerChoice);
        }

        private void SetState(BattleState newState)
        {
            switch (newState)
            {
                case BattleState.PlayerChoice:
                    StartPlayerChoicePhase();
                    break;
                case BattleState.BossAction:
                    StartCoroutine(StartBossActionPhase());
                    break;
                case BattleState.CheckBattleEnd:
                    break;
                default:
                    break;
            }
        }

        private void StartPlayerChoicePhase()
        {
            _turnCount++;
            
            _uiManager.ShowActionMenuForCurrentPlayer(_currentPlayerIndex);
        }

        public void OnActionChoice(ActionData newAction)
        {
            switch (newAction.Action)
            {
                case ActionType.Move:
                {
                    StartCoroutine(ProcessMove(newAction));
                    break;
                }
                
                default: break;
            }
        }
        
        public IEnumerator ProcessMove(ActionData newAction)
        {
            var source = _entities[(int)newAction.Source];
            var target = _entities[(int)newAction.Target];
            
            MoveInstance move = source.GetMoveInstance(newAction.ActionIndex);
            if (move == null)
            {
                yield break;
            }

            switch (move.Data.Category)
            {
                case MoveCategory.Single:
                {
                    // Battle Message
                    yield return StartCoroutine(_uiManager.ShowBattleMessage($"{source.EntityName}의 {move.Data.Name}!", 1f));
                    
                    // TODO: Implementing PlayMoveEffects()
                    yield return StartCoroutine(PlayMoveEffects(target));
                    
                    // Damage
                    int prevHP = target.CurrentHP;
                    target.TakeDamage(move.Data.Type, DamageFormula.GetRawHit(source, move));
                    int currHP = target.CurrentHP;
                    
                    // HP Animation
                    yield return StartCoroutine(_uiManager.AnimateHPBarDecrease((int)newAction.Target, prevHP, currHP));

                    break;
                }
                    
                default:
                    break;
            }
        }

        private IEnumerator PlayMoveEffects(BattleEntity target)
        {
            var effect = Instantiate(_testEffect, target.transform.position, target.transform.rotation);
            yield return new WaitForSeconds(4f);
        }

        IEnumerator StartBossActionPhase()
        {
            ActionData bossAction = DetermineBossChoice();
            yield break;
        }

        // ReSharper disable Unity.PerformanceAnalysis
        ActionData DetermineBossChoice()
        {
            if (_entities[(int)EntityType.Boss] is null)
            {
                Debug.LogWarning("Boss entity is not initialized.");
                return new ActionData();
            }
    
            BattleEntity boss = _entities[(int)EntityType.Boss];
    
            if (boss.MoveSet.Count == 0)
            {
                Debug.LogWarning("Boss has no moves.");
                return new ActionData();
            }
    
            int moveIndex = 0;
    
            return new ActionData(
                ActionType.Move,
                moveIndex,
                EntityType.Boss,
                EntityType.Character1
            );
        }

        float CalculateMoveScore(MoveInstance move, BattleEntity boss, List<BattleEntity> players)
        {
            if (move.CooldownLeft > 0 || move.UsageLeft == 0)
            {
                return 0f;
            }
            
            MoveData data = move.Data;
            
            float SingleScore() =>
                players.Max(p =>
                {
                    float raw = DamageFormula.GetExpectedRawDamage(boss, data);
                    float exp = p.PreviewMitigate(raw, data);

                    bool canKO = p.CurrentHP <= exp;
                    float bonus = canKO ? Weights.KillBonus * data.Accuracy : 0f;

                    return (exp + bonus) * Weights.SingleHit;
                });

            float AOEScore() =>
                players.Sum(p =>
                {
                    float raw = DamageFormula.GetExpectedRawDamage(boss, data);
                    float exp = p.PreviewMitigate(raw, data);

                    bool canKO = p.CurrentHP <= exp;
                    float bonus = canKO ? Weights.KillBonus * data.Accuracy : 0f;

                    return exp + bonus;
                }) * Weights.AOE;

            float MultiRandomScore() =>
                players.Max(p =>
                {
                    float raw = DamageFormula.GetExpectedRawDamage(boss, data) * Weights.MultiRandomHit;
                    float exp = p.PreviewMitigate(raw, data);
                    return exp;
                });

            float ChargeScore() =>
                players.Max(p =>
                {
                    float raw = DamageFormula.GetExpectedRawDamage(boss, data) * Weights.Charge;
                    float exp = p.PreviewMitigate(raw, data);
                    return exp;
                });

            return data.Category switch
            {
                MoveCategory.Single => SingleScore(),
                MoveCategory.AOE => AOEScore(),
                MoveCategory.MultiRandom => MultiRandomScore(),
                MoveCategory.Charge => ChargeScore(),
                MoveCategory.Buff => Weights.BuffBase,
                MoveCategory.Debuff => Weights.DebuffBase,
                MoveCategory.ClearOppBuff => Weights.ClearOppBuffBase,
                MoveCategory.ClearSelfDebuff => Weights.ClearSelfDebuffBase,
                MoveCategory.Stun => Weights.StunBase * data.Accuracy,
                _ => 0f
            };
        }
    }
}
