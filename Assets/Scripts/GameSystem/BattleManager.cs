using System;
using System.Collections;
using Camera;
using Data;
using Entity;
using UnityEngine;

namespace GameSystem
{
    public enum BattleState
    {
        PlayerChoice,
        BossAction,
        CheckBattleEnd,
        BattleEnded
    }

    public class BattleManager : MonoBehaviour
    {
        [SerializeField] private UIManager _uiManager;
        [SerializeField] private CameraManager _cameraManager;
        
        public AIWeights Weights;

        private BattleState _currentState;

        [SerializeField] private BattleEntity[] _entities = new BattleEntity[5];
        public BattleEntity[] Entities => _entities;

        private const int PlayerCount = 4; 
        private int _turnCount; 
        public int TurnCount => _turnCount;

        private float _disableAlpha = 0.1f;

        private int _currentPlayerIndex;

        public event Action OnEntitiesInitialized;

        [SerializeField] private GameObject _testEffect;

        private void Start()
        {
            _currentPlayerIndex = 0;
            OnEntitiesInitialized?.Invoke();

            ChangeBattleState(BattleState.PlayerChoice);
        }

        private void ChangeBattleState(BattleState newState)
        {
            _currentState = newState;
            switch (_currentState)
            {
                case BattleState.PlayerChoice:
                    BeginPlayerTurn(_currentPlayerIndex);
                    break;
                case BattleState.BossAction:
                    StartCoroutine(BeginBossTurn());
                    break;
                case BattleState.CheckBattleEnd:
                    break;
                case BattleState.BattleEnded:
                    HandleBattleEnd();
                    break;
                default:
                    break;
            }
        }

        private void BeginPlayerTurn(int playerIndex)
        {
            _turnCount++;
            _cameraManager.SwitchCameraTo((CineCamType)playerIndex);
            SetSpriteAlphaExclusive(playerIndex);
            _uiManager.ShowActionMenuForCurrentPlayer(playerIndex);
        }

        public void ReceivePlayerChoice(ActionData newAction)
        {
            StartCoroutine(ExecutePlayerAction(newAction));
        }

        private IEnumerator ExecutePlayerAction(ActionData action)
        {
            switch (action.Action)
            {
                case ActionType.Move:
                    yield return StartCoroutine(PerformMove(action));
                    break;
                default:
                    break;
            }

            if (CheckBattleEnd()) yield break;

            _currentPlayerIndex++;
            if (_currentPlayerIndex >= PlayerCount)
            {
                _currentPlayerIndex = 0;
                _cameraManager.SwitchCameraTo((CineCamType)4);
                SetSpriteAlphaExclusive(-1);
                ChangeBattleState(BattleState.BossAction);
            }
            else
            {
                SetSpriteAlphaExclusive(_currentPlayerIndex);
                ChangeBattleState(BattleState.PlayerChoice);
            }
        }

        private IEnumerator BeginBossTurn()
        {
            _cameraManager.SwitchCameraTo((CineCamType)4);
            SetSpriteAlphaExclusive(-1);

            var boss = _entities[4];
            BattleEntity[] players = new BattleEntity[4];
            for (int i = 0; i < 4; i++)
            {
                players[i] = _entities[i];
            }

            UtilityAI bossAI = new UtilityAI(boss, players, Weights);
            MoveDecision decision = bossAI.Decide();

            if (decision.MoveIndex >= 0)
            {
                ActionData bossAction = new ActionData(
                    ActionType.Move,
                    decision.MoveIndex,
                    EntityType.Boss,
                    decision.TargetEntity
                );
                yield return StartCoroutine(ExecuteBossAction(bossAction));
            }

            ChangeBattleState(BattleState.CheckBattleEnd);
        }

        private IEnumerator ExecuteBossAction(ActionData bossAction)
        {
            var boss = _entities[(int)EntityType.Boss];
            var move = boss.GetMoveInstance(bossAction.ActionIndex);
            move.UsageLeft--;
            move.CooldownLeft = move.Data.Cooldown;

            yield return StartCoroutine(PerformMove(bossAction));
            if (CheckBattleEnd()) yield break;
        }

        private IEnumerator PerformMove(ActionData action)
        {
            var source = _entities[(int)action.Source];
            var target = _entities[(int)action.Target];

            MoveInstance move = source.GetMoveInstance(action.ActionIndex);
            if (move == null) yield break;

            switch (move.Data.Category)
            {
                case MoveCategory.Single:
                    yield return ShowActionMessage(source, move.Data);
                    yield return SpawnMoveEffects(source, target);

                    int prevHP = target.CurrentHP;
                    target.TakeDamage(move.Data.Type, DamageFormula.GetRawHit(source, move));
                    int currHP = target.CurrentHP;

                    yield return StartCoroutine(
                        _uiManager.AnimateHPBarDecrease(
                            (int)action.Target, prevHP, currHP
                        )
                    );
                    break;
            }
        }

        private IEnumerator ShowActionMessage(BattleEntity source, MoveData moveData)
        {
            yield return StartCoroutine(
                _uiManager.ShowBattleMessage(
                    $"{source.EntityName} used {moveData.Name}!", 1f
                )
            );
        }

        private IEnumerator SpawnMoveEffects(BattleEntity source, BattleEntity target)
        {
            if (source is BossEntity)
            {
                var effect = Instantiate(_testEffect, 
                    target.transform.position, 
                    target.transform.rotation);
                yield return new WaitForSeconds(4f);
            }
            else
            {
                var effect = Instantiate(_testEffect, 
                    target.transform.position, 
                    target.transform.rotation);
                int newLayer = LayerMask.NameToLayer("BossEffect");
                effect.layer = newLayer;
                yield return new WaitForSeconds(4f);
            }
        }

        private bool CheckBattleEnd()
        {
            var boss = _entities[4];
            if (boss.CurrentHP <= 0)
            {
                Debug.Log("Battle End: Players Win!");
                return true;
            }

            bool allPlayersDead = true;
            for (int i = 0; i < 4; i++)
            {
                if (_entities[i].CurrentHP > 0)
                {
                    allPlayersDead = false;
                    break;
                }
            }
            if (allPlayersDead)
            {
                Debug.Log("Battle End: Boss Wins!");
                return true;
            }

            return false;
        }

        private void HandleBattleEnd()
        {
            StartCoroutine(_uiManager.ShowBattleMessage("Battle Ended"));
        }

        private void SetSpriteAlphaExclusive(int activeIndex)
        {
            for (int i = 0; i < PlayerCount; i++)
            {
                var spriteRenderer = _entities[i].SpriteRenderer;
                if (!spriteRenderer) continue;

                var color = spriteRenderer.color;
                if (activeIndex < 0)
                {
                    color.a = 1.0f;
                }
                else
                {
                    color.a = (i == activeIndex) ? 1.0f : _disableAlpha;
                }
                spriteRenderer.color = color;
            }
        }
    }
}
