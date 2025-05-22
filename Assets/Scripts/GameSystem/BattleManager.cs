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

        public int disableAlpha = 100;

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

            // DisableSprite(0, 1, 2, 3);
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
            
            _cameraManager.SwitchCameraTo((CineCamType)_currentPlayerIndex);
            
            EnableSprite(_currentPlayerIndex);
            
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

            if (_currentPlayerIndex >= 4)
            {
                _currentPlayerIndex = 0;
                _cameraManager.SwitchCameraTo((CineCamType)4); // ZoomOut
                SetState(BattleState.BossAction);
            }
            else
            {
                DisableSprite(_currentPlayerIndex - 1);
                EnableSprite(_currentPlayerIndex);
                StartPlayerChoicePhase();
            }
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
            _cameraManager.SwitchCameraTo((CineCamType)4);
            
            var boss = _entities[(int)EntityType.Boss];
            BattleEntity[] players = new BattleEntity[4];
            for (int i = 0; i < 4; i++)
            {
                players[i] = _entities[i];
            }
            
            EnableSprite(0, 1, 2, 3);

            UtilityAI bossAI = new UtilityAI(boss, players, Weights);

            MoveDecision decision = bossAI.Decide();

            if (decision.MoveIndex < 0)
            {
                Debug.Log("No valid boss moves.");
                yield break;
            }
            else
            {
                ActionData bossAction = new ActionData(
                    ActionType.Move,
                    decision.MoveIndex,
                    EntityType.Boss,
                    decision.TargetEntity
                );

                yield return StartCoroutine(HandleBossActionChoice(bossAction));
            }
            
            DisableSprite(0, 1, 2, 3);
            
            SetState(BattleState.CheckBattleEnd);
        }

        private IEnumerator HandleBossActionChoice(ActionData bossAction)
        {
            var boss = _entities[(int)EntityType.Boss];
            var move = boss.GetMoveInstance(bossAction.ActionIndex);

            move.UsageLeft--;
            move.CooldownLeft = move.Data.Cooldown;

            yield return StartCoroutine(ProcessMove(bossAction));
        }

        public void DisableSprite(params int[] entityIndices)
        {
            foreach (int index in entityIndices)
            {
                var spriteColor = _entities[index].SpriteRenderer.color;
                spriteColor.a = disableAlpha;
                _entities[index].SpriteRenderer.color = spriteColor;
            }
        }
        
        public void EnableSprite(params int[] entityIndices)
        {
            foreach (int index in entityIndices)
            {
                var spriteColor = _entities[index].SpriteRenderer.color;
                spriteColor.a = 255;
                _entities[index].SpriteRenderer.color = spriteColor;
            }
        }
    }
}
