using System.Collections;
using UnityEngine;
using Entity;
using AI;
using Data;
using CameraSystem;
using GameSystem.UI;

namespace GameSystem
{
    public class BattleManager : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private BattleSettings _battleSettings;
        
        [Header("Components")]
        [SerializeField] private CameraManager _cameraManager;
        [SerializeField] private AIWeights _aiWeights;
        
        [Header("UI References")]
        [SerializeField] private BattleUIController _battleUIController;
        [SerializeField] private StatusUIManager _statusUIManager;
        [SerializeField] private MessageUIManager _messageUIManager;
        
        [Header("Entity Prefabs")]
        [SerializeField] private GameObject[] _playerPrefabs = new GameObject[4];
        [SerializeField] private GameObject _bossPrefab;
        
        [Header("Boss Configuration")]
        [SerializeField] private ShieldPattern _defaultShieldPattern;
        [SerializeField] private MoveData _bossShieldMove;
        
        [Header("Spawn Configuration")]
        [SerializeField] private Transform[] _playerSpawnPoints;
        [SerializeField] private Transform _bossSpawnPoint;
        [SerializeField] private Transform _entityContainer;
        
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
            
            if (_battleUIController == null)
                _battleUIController = FindAnyObjectByType<BattleUIController>();
            if (_statusUIManager == null)
                _statusUIManager = FindAnyObjectByType<StatusUIManager>();
            if (_messageUIManager == null)
                _messageUIManager = FindAnyObjectByType<MessageUIManager>();
                
            _actionExecutor.SetUIReferences(_battleUIController, _statusUIManager, _messageUIManager);
            _effectProcessor.SetUIReferences(_battleUIController, _statusUIManager, _messageUIManager);
            _turnManager.SetBattleManager(this);
        }
        
        private void SubscribeToEvents()
        {
            _stateMachine.OnStateChanged += HandleStateChanged;
            _actionQueue.OnQueueEmpty += HandleQueueEmpty;
        }
        
        private void UnsubscribeFromEvents()
        {
            if (_stateMachine != null)
                _stateMachine.OnStateChanged -= HandleStateChanged;
                
            if (_actionQueue != null)
                _actionQueue.OnQueueEmpty -= HandleQueueEmpty;
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
            
            if (_statusUIManager != null)
                _statusUIManager.Initialize(_entities);
                
            if (_battleUIController != null)
                _battleUIController.OnBattleStarted(_entities);
            
            yield return new WaitForSeconds(_battleSettings.TurnStartDelay);
            
            _stateMachine.TransitionTo(BattleState.PlayerChoice);
        }
        
        private void CreateEntities()
        {
            _entities = new BattleEntity[GameConstants.Battle.TOTAL_ENTITIES];
            
            if (_entityContainer == null)
            {
                GameObject containerObj = new GameObject("EntityContainer");
                _entityContainer = containerObj.transform;
            }
            
            for (int i = 0; i < GameConstants.Battle.PLAYER_COUNT; i++)
            {
                if (i < _playerPrefabs.Length && _playerPrefabs[i] != null && i < _playerSpawnPoints.Length)
                {
                    GameObject playerObj = Instantiate(_playerPrefabs[i], _playerSpawnPoints[i].position, Quaternion.identity, _entityContainer);
                    BattleEntity entity = playerObj.GetComponent<BattleEntity>();
                    
                    if (entity != null)
                    {
                        entity.Initialize();
                        _entities[i] = entity;
                    }
                    else
                    {
                        Debug.LogError($"Player prefab at index {i} missing BattleEntity component");
                        Destroy(playerObj);
                    }
                }
            }
            
            if (_bossPrefab != null && _bossSpawnPoint != null)
            {
                GameObject bossObj = Instantiate(_bossPrefab, _bossSpawnPoint.position, Quaternion.identity, _entityContainer);
                BossEntity boss = bossObj.GetComponent<BossEntity>();
                
                if (boss != null)
                {
                    ConfigureBoss(boss);
                    boss.Initialize();
                    _entities[GameConstants.Battle.BOSS_INDEX] = boss;
                }
                else
                {
                    Debug.LogError("Boss prefab missing BossEntity component");
                    Destroy(bossObj);
                }
            }
        }
        
        private void ConfigureBoss(BossEntity boss)
        {
            if (BossContainer.Instance != null)
            {
                var container = BossContainer.Instance;
                
                if (!string.IsNullOrEmpty(container.CurrentBossName))
                {
                    boss.EntityName = container.CurrentBossName;
                }
                
                boss.ElementType = container.CurrentBossType;
                
                if (container.CurrentBossImageData != null)
                {
                    SetBossSprite(boss, container.CurrentBossImageData);
                }
            }
            
            if (_defaultShieldPattern != null)
            {
                var shieldPatternField = typeof(BossEntity).GetField("_shieldPattern", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                if (shieldPatternField != null)
                {
                    shieldPatternField.SetValue(boss, _defaultShieldPattern);
                }
            }
            
            EnsureBossHasShieldMove(boss);
        }
        
        private void SetBossSprite(BossEntity boss, byte[] imageData)
        {
            var texture = new Texture2D(2, 2);
            if (!texture.LoadImage(imageData)) return;
            
            var sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                0.7f
            );
            
            var spriteRenderer = boss.GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = sprite;
            }
        }
        
        private void EnsureBossHasShieldMove(BossEntity boss)
        {
            bool hasShieldMove = false;
            
            foreach (var move in boss.MoveSet)
            {
                if (move == null) continue;
                
                foreach (var effect in move.Effects)
                {
                    if (effect.EffectType == MoveEffectType.Shield)
                    {
                        hasShieldMove = true;
                        break;
                    }
                }
                
                if (hasShieldMove) break;
            }
            
            if (!hasShieldMove && _bossShieldMove != null)
            {
                boss.MoveSet.Add(_bossShieldMove);
            }
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
                        null
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
            else
            {
                if (_battleUIController != null)
                {
                    _battleUIController.ShowActionMenuForPlayer(playerIndex);
                }
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
        
        public void OnActionSelected(ActionType actionType, int actionIndex, int playerIndex)
        {
            if (actionType == ActionType.Move)
            {
                var entity = _entities[playerIndex];
                var moveData = entity.GetMoveData(actionIndex);

                EntityType defaultTarget = EntityType.Boss;
                if (moveData != null && moveData.AllowedTargetSide == TargetSide.Self)
                {
                    defaultTarget = (EntityType)playerIndex;
                }

                _pendingAction = new ActionData(
                    actionType,
                    actionIndex,
                    (EntityType)playerIndex,
                    defaultTarget
                );

                return;
            }
            else
            {
                var action = new ActionData(
                    actionType,
                    actionIndex,
                    (EntityType)playerIndex,
                    EntityType.Boss
                );
                EnqueuePlayerAction(action);
            }
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
            if (_messageUIManager != null)
                _messageUIManager.ShowMessage($"{entity.EntityName} is stunned!", _battleSettings.MessageDuration);
            yield return new WaitForSeconds(_battleSettings.MessageDuration);
        }
        
        public void QueueCounterAttack(BattleEntity counter)
        {
            var boss = _entities[GameConstants.Battle.BOSS_INDEX];
            if (boss.CurrentHP > 0)
            {
                _actionQueue.EnqueueAction(
                    new ActionData(),
                    () => ExecuteCounterAttack(counter, boss),
                    null
                );
            }
        }
        
        private IEnumerator ExecuteCounterAttack(BattleEntity counter, BattleEntity boss)
        {
            if (_messageUIManager != null)
                _messageUIManager.ShowMessage($"{counter.EntityName} counters!", 1f);
            yield return new WaitForSeconds(1f);
            
            int damage = Mathf.RoundToInt(counter.Attack * 0.5f);
            boss.TakeDamage(counter.ElementType, damage);
            
            if (_effectProcessor != null)
                _effectProcessor.OnDamageDealt(counter, boss, damage, counter.ElementType);
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
        
        public void OnEntityDefeated(BattleEntity entity)
        {
            _actionQueue.EnqueueAction(
                new ActionData(),
                () => WaitAndCheckBattleEnd(),
                null
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
            
            if (_battleUIController != null)
                _battleUIController.OnBattleEnded(playerWon);
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
            
            if (_entityContainer != null)
            {
                foreach (Transform child in _entityContainer)
                {
                    Destroy(child.gameObject);
                }
            }
        }
        
        public void OnTurnStarted(BattleEntity entity, int turnNumber)
        {
        }
    }
}