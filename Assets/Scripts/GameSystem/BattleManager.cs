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
        public AIWeights Weights;
        
        private BattleState _currentState;
        private List<ActionData> _currentPlayerChoices = new List<ActionData>();

        [SerializeField] private BattleEntity[] _entities = new BattleEntity[5];
        
        private const int PlayerCount = 4; // 4 players
        
        private int _turnCount = 0;

        private void Start()
        {
            SetState(BattleState.BossAction);
        }

        private void SetState(BattleState newState)
        {
            switch (newState)
            {
                case BattleState.PlayerChoice:
                    StartPlayerChoicePhase();
                    break;
                case BattleState.PlayerAction:
                    StartCoroutine(StartPlayerActionPhase());
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

        void StartPlayerChoicePhase()
        {
            _turnCount++;
            
            // TODO: Bind UI events
            // then call HandleAllChoicesComplete();
        }

        void HandleAllChoicesComplete(List<ActionData> choices)
        {
            _currentPlayerChoices = choices;
            SetState(BattleState.PlayerAction);
        }

        IEnumerator StartPlayerActionPhase()
        {
            // TODO: Call UI functions...

            foreach (var choice in _currentPlayerChoices)
            {
                var sourceEntity = _entities[(int)choice.Source];
                var targetEntity = _entities[(int)choice.Target];

                switch (choice.Action)
                {
                    case ActionType.Move:
                    {
                        var moveInstance = sourceEntity.GetMoveInstance(choice.ActionIndex);
                        targetEntity.TakeDamage(moveInstance.Data.Type, DamageFormula.GetRawHit(sourceEntity, moveInstance));
                        break;
                    }
                    case ActionType.Item:
                    {
                        // TODO: Item use logic
                        break;
                    }
                    default:
                    {
                        break;
                    }
                }
            }
            
            SetState(BattleState.BossAction);
            yield break;
        }

        IEnumerator StartBossActionPhase()
        {
            ActionData bossAction = DetermineBossChoice();
            yield break;
        }

        ActionData DetermineBossChoice()
        {
            // Evaluate moves
            List<MoveData> bossMoveSet = _entities[(int)EntityType.Boss].MoveSet;

            return new ActionData();
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
