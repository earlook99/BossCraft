using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Camera;
using Data;
using Entity;
using UnityEngine;

namespace GameSystem
{
    /// <summary>
    /// Defines the possible states of a battle.
    /// </summary>
    public enum BattleState
    {
        /// <summary>
        /// The player is choosing an action.
        /// </summary>
        PlayerChoice,
        /// <summary>
        /// The player's chosen action is being executed.
        /// </summary>
        PlayerAction, // Currently unused, but defined for potential future states
        /// <summary>
        /// The boss is performing an action.
        /// </summary>
        BossAction,
        /// <summary>
        /// The battle is checking for end conditions (e.g., all players defeated, boss defeated).
        /// </summary>
        CheckBattleEnd
    }

    /// <summary>
    /// Manages the overall flow and state of a battle.
    /// This includes turn management, action handling, and game state transitions.
    /// </summary>
    public class BattleManager : MonoBehaviour
    {
        [SerializeField] private UIManager _uiManager;
        [SerializeField] private CameraManager _cameraManager;
        
        /// <summary>
        /// Weights used by the boss AI to determine move scores.
        /// </summary>
        public AIWeights Weights;
        
        private BattleState _currentState;

        [SerializeField] private BattleEntity[] _entities = new BattleEntity[5];
        /// <summary>
        /// Gets the array of all battle entities participating in the battle.
        /// Index 0-3 for players, Index 4 for Boss. See <see cref="EntityType"/> for indexing.
        /// </summary>
        public BattleEntity[] Entities => _entities;

        private const int PlayerCount = 4; // Number of player characters
        
        private int _turnCount = 0;
        /// <summary>
        /// Gets the current turn count of the battle.
        /// </summary>
        public int TurnCount => _turnCount;

        private int _currentPlayerIndex; // Index of the current player character whose turn it is
        
        /// <summary>
        /// Event triggered when all battle entities have been initialized.
        /// </summary>
        public event Action OnEntitiesInitialized;

        [SerializeField] private GameObject _testEffect; // Temporary for visual effect testing

        /// <summary>
        /// Called when the script instance is being loaded.
        /// Initializes the current player index, invokes entity initialization event, and sets the initial battle state.
        /// </summary>
        private void Start()
        {
            _currentPlayerIndex = 0;
            
            OnEntitiesInitialized?.Invoke();
            
            SetState(BattleState.PlayerChoice);
        }

        /// <summary>
        /// Sets the current state of the battle and triggers corresponding phase logic.
        /// </summary>
        /// <param name="newState">The new battle state to transition to.</param>
        private void SetState(BattleState newState)
        {
            _currentState = newState;
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

        /// <summary>
        /// Starts the player choice phase, incrementing the turn count and showing the action menu.
        /// </summary>
        private void StartPlayerChoicePhase()
        {
            _turnCount++;
            
            _uiManager.ShowActionMenuForCurrentPlayer(_currentPlayerIndex);
        }

        /// <summary>
        /// Called when a player makes an action choice. Starts the action handling coroutine.
        /// </summary>
        /// <param name="newAction">The action chosen by the player.</param>
        public void OnActionChoice(ActionData newAction)
        {
            StartCoroutine(HandleActionChoice(newAction));
        }

        /// <summary>
        /// Coroutine to handle the execution of a chosen player action.
        /// </summary>
        /// <param name="newAction">The action data.</param>
        /// <returns>An IEnumerator for the coroutine.</returns>
        public IEnumerator HandleActionChoice(ActionData newAction)
        {
            switch (newAction.Action)
            {
                case ActionType.Move:
                {
                    yield return StartCoroutine(ProcessMove(newAction));
                    break;
                }
                
                default: break;
            }
            
            //
            _currentPlayerIndex++;
            _cameraManager.SwitchCameraTo(CineCamType.BattleOutZoom);
            StartPlayerChoicePhase();
        }
        
        /// <summary>
        /// Coroutine to process a move action.
        /// Includes displaying battle messages, playing effects, calculating damage, and animating HP.
        /// </summary>
        /// <param name="newAction">The move action data.</param>
        /// <returns>An IEnumerator for the coroutine.</returns>
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

        /// <summary>
        /// Coroutine to play visual effects for a move on a target.
        /// Currently uses a placeholder test effect.
        /// </summary>
        /// <param name="target">The target entity for the visual effect.</param>
        /// <returns>An IEnumerator for the coroutine.</returns>
        private IEnumerator PlayMoveEffects(BattleEntity target)
        {
            var effect = Instantiate(_testEffect, target.transform.position, target.transform.rotation);
            yield return new WaitForSeconds(4f);
        }

        /// <summary>
        /// Coroutine to start the boss's action phase.
        /// Currently determines the boss choice but doesn't fully execute it yet.
        /// </summary>
        /// <returns>An IEnumerator for the coroutine.</returns>
        IEnumerator StartBossActionPhase()
        {
            ActionData bossAction = DetermineBossChoice();
            // TODO: Implement full boss action execution based on bossAction
            yield break;
        }

        // ReSharper disable Unity.PerformanceAnalysis
        /// <summary>
        /// Determines the boss's next action.
        /// Currently selects the first move if available.
        /// </summary>
        /// <returns>The <see cref="ActionData"/> for the boss's chosen action.</returns>
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

        /// <summary>
        /// Calculates a score for a given move for the boss AI.
        /// This score helps the AI decide which move to use.
        /// </summary>
        /// <param name="move">The move instance to score.</param>
        /// <param name="boss">The boss entity performing the move.</param>
        /// <param name="players">A list of player entities who could be targets.</param>
        /// <returns>A float score representing the desirability of the move.</returns>
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
                    float exp = p.PreviewMitigate(raw, data); // Expected damage after mitigation

                    bool canKO = p.CurrentHP <= exp;
                    float bonus = canKO ? Weights.KillBonus * data.Accuracy : 0f; // Bonus for potential KO

                    return (exp + bonus) * Weights.SingleHit;
                });

            float AOEScore() =>
                players.Sum(p =>
                {
                    float raw = DamageFormula.GetExpectedRawDamage(boss, data);
                    float exp = p.PreviewMitigate(raw, data); // Expected damage after mitigation for this player

                    bool canKO = p.CurrentHP <= exp;
                    float bonus = canKO ? Weights.KillBonus * data.Accuracy : 0f; // Bonus for potential KO on this player

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
