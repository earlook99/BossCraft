using System.Collections;
using UnityEngine;
using Entity;
using AI;
using Data;
using CameraSystem;
using GameSystem.Events;
using GameSystem.Factory;
using GameSystem.Pooling;

namespace GameSystem
{
    public class BattleManager : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private BattleSettings _battleSettings;
        
        [Header("Components")]
        [SerializeField] private CameraManager _cameraManager;
        [SerializeField] private AIWeights _aiWeights;
        
        [Header("Entity Configuration")]
        [SerializeField] private string[] _playerEntityNames;
        [SerializeField] private Transform[] _playerSpawnPoints;
        [SerializeField] private Transform _bossSpawnPoint;
        
        private BattleStateMachine _stateMachine;
        private TurnManager _turnManager;
        private ActionExecutor _actionExecutor;
        private BattleEffectProcessor _effectProcessor;
        private ActionQueue _actionQueue;
        private BattleContext _battleContext;
        
        private BattleEntity[] _entities;
        private ActionData _pendingAction;
        
        private void Awake()
        {
            InitializeComponents();
            SubscribeToEvents();
        }
        
        private void Start()
        {
            StartCoroutine(InitializeBattle());
        }
        
        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            CleanupBattle();
        }
        
        private void InitializeComponents()
        {
            _battleContext = GetComponent<BattleContext>();
            if (_battleContext == null)
            {
                _battleContext = gameObject.AddComponent<BattleContext>();
            }
            
            _stateMachine = gameObject.AddComponent<BattleStateMachine>();
            _turnManager = gameObject.AddComponent<TurnManager>();
            _actionExecutor = gameObject.AddComponent<ActionExecutor>();
            _effectProcessor = gameObject.AddComponent<BattleEffectProcessor>();
            _actionQueue = gameObject.AddComponent<ActionQueue>();
        }
        
        private void SubscribeToEvents()
        {
            _stateMachine.OnStateChanged += HandleStateChanged;
            _actionQueue.OnQueueEmpty += HandleQueueEmpty;
            
            BattleEvents.OnEntityDefeated += HandleEntityDefeated;
            UIEvents.OnActionSelected += HandleActionSelected;
        }
        
        private void UnsubscribeFromEvents()
        {
            if (_stateMachine != null)
                _stateMachine.OnStateChanged -= HandleStateChanged;
                
            if (_actionQueue != null)
                _actionQueue.OnQueueEmpty -= HandleQueueEmpty;
                
            BattleEvents.OnEntityDefeated -= HandleEntityDefeated;
            UIEvents.OnActionSelected -= HandleActionSelected;
            
            BattleEvents.ClearAllListeners();
            UIEvents.ClearAllListeners();
        }
        
        private IEnumerator InitializeBattle()
        {
            yield return new WaitForSeconds(0.1f);
            
            CreateEntities();
            
            if (_entities == null || _entities.Length == 0)
            {
                Debug.LogError("Failed to create entities");
                yield break;
            }
            
            _battleContext.Initialize(_entities, _stateMachine, _turnManager);
            _turnManager.Initialize(_entities);
            _actionExecutor.Initialize(_entities);
            _effectProcessor.Initialize(_entities, null);
            
            BattleEvents.RaiseBattleStarted(_entities);
            
            yield return new WaitForSeconds(_battleSettings.TurnStartDelay);
            
            _stateMachine.TransitionTo(BattleState.PlayerChoice);
        }
        
        private void CreateEntities()
        {
            var entityFactory = EntityFactory.Instance;
            if (entityFactory == null)
            {
                Debug.LogError("EntityFactory not found");
                return;
            }
            
            Vector3[] playerPositions = new Vector3[GameConstants.Battle.PLAYER_COUNT];
            for (int i = 0; i < _playerSpawnPoints.Length && i < playerPositions.Length; i++)
            {
                playerPositions[i] = _playerSpawnPoints[i].position;
            }
            
            Vector3 bossPosition = _bossSpawnPoint != null ? _bossSpawnPoint.position : Vector3.zero;
            
            _entities = entityFactory.CreateBattleEntities(_playerEntityNames, playerPositions, bossPosition);
        }
        
        private void HandleStateChanged(BattleState previousState, BattleState newState)
        {
            switch (newState)
            {
                case BattleState.PlayerChoice:
                    HandlePlayerChoice();
                    break;
                case BattleState.BossAction:
                    _actionQueue.EnqueueAction(
                        new ActionData(),
                        () => HandleBossTurn(),
                        0f
                    );
                    break;
                case BattleState.CheckBattleEnd:
                    CheckBattleEnd();
                    break;
                case BattleState.BattleEnded:
                    HandleBattleEnd();
                    break;
            }
        }
        
        private void HandlePlayerChoice()
        {
            var currentEntity = _turnManager.CurrentEntity;
            if (currentEntity == null) return;
            
            int playerIndex = _turnManager.GetCurrentPlayerIndex();
            if (playerIndex < 0) return;
            
            _cameraManager.SwitchCameraTo((CineCamType)playerIndex);
            SetSpriteAlphaExclusive(playerIndex);
            
            if (currentEntity.IsStunned)
            {
                _actionQueue.EnqueueAction(
                    new ActionData(),
                    () => ShowStunnedMessage(currentEntity),
                    0f,
                    (success) => AdvanceTurn()
                );
            }
            else if (currentEntity.IsCharging)
            {
                var autoAction = new ActionData(
                    ActionType.Move,
                    currentEntity.ChargingMoveIndex,
                    (EntityType)playerIndex,
                    currentEntity.ChargingMoveTarget
                );
                EnqueuePlayerAction(autoAction);
            }
        }
        
        private IEnumerator HandleBossTurn()
        {
            _cameraManager.SwitchCameraTo(CineCamType.ZoomOut);
            SetSpriteAlphaExclusive(-1);
            
            yield return new WaitForSeconds(_battleSettings.TurnTransitionDelay);
            
            var boss = _turnManager.GetBoss();
            if (boss == null) yield break;
            
            ActionData bossAction;
            
            if (boss.IsCharging)
            {
                bossAction = new ActionData(
                    ActionType.Move,
                    boss.ChargingMoveIndex,
                    EntityType.Boss,
                    boss.ChargingMoveTarget
                );
            }
            else
            {
                var players = _turnManager.GetRemainingPlayers().ToArray();
                var bossAI = new UtilityAI(boss, players, _aiWeights);
                var decision = bossAI.Decide();
                
                if (decision.MoveIndex < 0) yield break;
                
                bossAction = new ActionData(
                    ActionType.Move,
                    decision.MoveIndex,
                    EntityType.Boss,
                    decision.TargetEntity
                );
            }
            
            yield return _actionExecutor.ExecuteAction(bossAction);
            
            _stateMachine.TransitionTo(BattleState.CheckBattleEnd);
        }
        
        private void HandleActionSelected(ActionSelectedEventArgs args)
        {
            _pendingAction = new ActionData(
                args.ActionType,
                args.ActionIndex,
                (EntityType)args.PlayerIndex,
                EntityType.Boss
            );
            
            if (args.ActionType == ActionType.Move)
            {
                var entity = _entities[args.PlayerIndex];
                var moveData = entity.GetMoveData(args.ActionIndex);
                
                if (moveData != null && moveData.Category != MoveCategory.AOE)
                {
                    return;
                }
            }
            
            EnqueuePlayerAction(_pendingAction);
        }
        
        public void OnTargetSelected(EntityType target)
        {
            if (_pendingAction.Source == EntityType.Boss) return;
            
            _pendingAction.Target = target;
            EnqueuePlayerAction(_pendingAction);
        }
        
        private void EnqueuePlayerAction(ActionData action)
        {
            _actionQueue.EnqueueAction(
                action,
                () => ExecutePlayerAction(action),
                0f,
                (success) => AdvanceTurn()
            );
        }
        
        private IEnumerator ExecutePlayerAction(ActionData action)
        {
            _stateMachine.TransitionTo(BattleState.ExecutingAction);
            yield return _actionExecutor.ExecuteAction(action);
        }
        
        private IEnumerator ShowStunnedMessage(BattleEntity entity)
        {
            entity.StartTurn();
            UIEvents.RaiseShowMessage($"{entity.EntityName} is stunned!", _battleSettings.MessageDuration);
            yield return new WaitForSeconds(_battleSettings.MessageDuration);
        }
        
        private void AdvanceTurn()
        {
            _turnManager.EndCurrentTurn();
            
            if (!_turnManager.NextTurn())
            {
                if (_turnManager.IsRoundComplete())
                {
                    _turnManager.StartNewRound();
                    _stateMachine.TransitionTo(BattleState.PlayerChoice);
                }
                else
                {
                    _stateMachine.TransitionTo(BattleState.BossAction);
                }
            }
            else
            {
                _stateMachine.TransitionTo(BattleState.PlayerChoice);
            }
        }
        
        private void HandleQueueEmpty()
        {
            if (_stateMachine.CurrentState == BattleState.ExecutingAction)
            {
                CheckBattleEnd();
            }
        }
        
        private void HandleEntityDefeated(EntityDefeatedEventArgs args)
        {
            _actionQueue.EnqueueAction(
                new ActionData(),
                () => WaitAndCheckBattleEnd(),
                float.MaxValue
            );
        }
        
        private IEnumerator WaitAndCheckBattleEnd()
        {
            yield return new WaitForSeconds(0.5f);
            CheckBattleEnd();
        }
        
        private void CheckBattleEnd()
        {
            bool bossDefeated = _entities[GameConstants.Battle.BOSS_INDEX].CurrentHP <= 0;
            bool allPlayersDefeated = true;
            
            for (int i = 0; i < GameConstants.Battle.PLAYER_COUNT; i++)
            {
                if (_entities[i].CurrentHP > 0)
                {
                    allPlayersDefeated = false;
                    break;
                }
            }
            
            if (bossDefeated || allPlayersDefeated)
            {
                _stateMachine.TransitionTo(BattleState.BattleEnded);
            }
            else if (_stateMachine.CurrentState == BattleState.CheckBattleEnd)
            {
                _turnManager.StartNewRound();
                _stateMachine.TransitionTo(BattleState.PlayerChoice);
            }
        }
        
        private void HandleBattleEnd()
        {
            _actionQueue.Clear();
            
            bool playerWon = _entities[GameConstants.Battle.BOSS_INDEX].CurrentHP <= 0;
            BattleEvents.RaiseBattleEnded(playerWon);
        }
        
        private void SetSpriteAlphaExclusive(int activeIndex)
        {
            for (int i = 0; i < GameConstants.Battle.PLAYER_COUNT; i++)
            {
                var sr = _entities[i].SpriteRenderer;
                if (!sr) continue;
                
                var color = sr.color;
                color.a = (activeIndex < 0 || i == activeIndex) 
                    ? GameConstants.UI.ACTIVE_SPRITE_ALPHA 
                    : GameConstants.UI.INACTIVE_SPRITE_ALPHA;
                sr.color = color;
            }
        }
        
        private void CleanupBattle()
        {
            _actionQueue?.Clear();
            
            var entityFactory = EntityFactory.Instance;
            if (entityFactory != null)
            {
                entityFactory.DestroyAllEntities();
            }
        }
    }
}