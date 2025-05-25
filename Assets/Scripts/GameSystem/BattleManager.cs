using System;
using System.Collections;
using CameraSystem;
using Data;
using Entity;
using Unity.Cinemachine; // CinemachineBrain
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

        private int _currentPlayerIndex;

        public event Action OnEntitiesInitialized;

        [SerializeField] private GameObject _testEffect;

        private void Start()
        {
            _currentPlayerIndex = 0;
            OnEntitiesInitialized?.Invoke();

            // 초기에 PlayerChoice 상태로 전환
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
                    if (!CheckBattleEnd())
                    {
                        _currentPlayerIndex = 0;
                        ChangeBattleState(BattleState.PlayerChoice);
                    }
                    break;

                case BattleState.BattleEnded:
                    HandleBattleEnd();
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
            }

            if (CheckBattleEnd()) 
                yield break; 

            _currentPlayerIndex++;
            if (_currentPlayerIndex >= PlayerCount)
            {
                // 플레이어 4명 행동이 끝나면 BossAction으로
                _currentPlayerIndex = 0;
                _cameraManager.SwitchCameraTo(CineCamType.ZoomOut);
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
            // 1) 보스 AI 결정 및 행동
            var boss = _entities[4];
            var players = new BattleEntity[4];
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

            // 2) 보스 턴이 끝나면 그냥 Player1 카메라 복귀 (즉시)
            _cameraManager.SwitchCameraTo(CineCamType.Player1);

            // 3) 전투 흐름 재개
            ChangeBattleState(BattleState.CheckBattleEnd);
        }

        private IEnumerator ExecuteBossAction(ActionData bossAction)
        {
            var boss = _entities[(int)EntityType.Boss];
            var move = boss.GetMoveInstance(bossAction.ActionIndex);
            move.UsageLeft--;
            move.CooldownLeft = move.Data.Cooldown;

            yield return StartCoroutine(PerformMove(bossAction));

            if (CheckBattleEnd()) 
                yield break;
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
                    $"{source.EntityName} used {moveData.Name}!",
                    1f
                )
            );
        }

        private IEnumerator SpawnMoveEffects(BattleEntity source, BattleEntity target)
        {
            if (source is BossEntity)
            {
                var effect = Instantiate(_testEffect, target.transform.position, target.transform.rotation);
                yield return new WaitForSeconds(2f);
            }
            else
            {
                var effect = Instantiate(_testEffect, target.transform.position, target.transform.rotation);
                int newLayer = LayerMask.NameToLayer("BossEffect");
                effect.layer = newLayer;
                yield return new WaitForSeconds(2f);
            }
        }

        private bool CheckBattleEnd()
        {
            var boss = _entities[4];
            if (boss.CurrentHP <= 0)
            {
                Debug.Log("Battle End: Players Win!");
                ChangeBattleState(BattleState.BattleEnded);
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
                ChangeBattleState(BattleState.BattleEnded);
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
                color.a = (activeIndex < 0)
                    ? 1.0f
                    : ((i == activeIndex) ? 1.0f : 0.1f);
                spriteRenderer.color = color;
            }
        }
    }
}
