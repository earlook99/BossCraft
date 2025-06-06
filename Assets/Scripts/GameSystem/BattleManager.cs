using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Entity;
using AI;
using Data;
using CameraSystem;
using UI;

namespace GameSystem
{
    public class BattleManager : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private BattleSettings battleSettings;
        [SerializeField] private AIWeights aiWeights;
        
        [Header("Components")]
        [SerializeField] private CameraManager cameraManager;
        [SerializeField] private BattleUIController uiController;
        
        [Header("Entity Prefabs")]
        [SerializeField] private GameObject[] playerPrefabs = new GameObject[4];
        [SerializeField] private GameObject bossPrefab;
        
        [Header("Boss Configuration")]
        [SerializeField] private ShieldPattern defaultShieldPattern;
        [SerializeField] private MoveData bossShieldMove;
        
        [Header("Spawn Configuration")]
        [SerializeField] private Transform[] playerSpawnPoints;
        [SerializeField] private Transform bossSpawnPoint;
        [SerializeField] private Transform entityContainer;
        
        [Header("Effects")]
        [SerializeField] private GameObject defaultEffectPrefab;
        
        private enum BattleState
        {
            Initializing,
            PlayerChoice,
            BossAction,
            ExecutingAction,
            CheckBattleEnd,
            BattleEnded
        }
        
        private BattleState currentState;
        private BattleEntity[] entities;
        private List<BattleEntity> turnOrder;
        private int currentTurnIndex;
        private int turnCount;
        
        private ActionData pendingAction;
        private Queue<System.Func<IEnumerator>> actionQueue = new Queue<System.Func<IEnumerator>>();
        private bool isProcessingQueue;
        
        private List<int> validTargetIndices = new List<int>(5);
        
        private const int PLAYER_COUNT = 4;
        private const int BOSS_INDEX = 4;
        private const int TOTAL_ENTITIES = 5;
        
        private void Awake()
        {
            if (cameraManager == null)
                cameraManager = FindAnyObjectByType<CameraManager>();
            if (uiController == null)
                uiController = FindAnyObjectByType<BattleUIController>();
        }
        
        private void Start()
        {
            StartCoroutine(InitializeBattle());
        }
        
        private IEnumerator InitializeBattle()
        {
            currentState = BattleState.Initializing;
            
            yield return new WaitForSeconds(0.1f);
            
            CreateEntities();
            InitializeTurnOrder();
            
            uiController.Initialize(entities, this);
            
            yield return new WaitForSeconds(battleSettings.TurnStartDelay);
            
            TransitionToState(BattleState.PlayerChoice);
        }
        
        private void CreateEntities()
        {
            entities = new BattleEntity[TOTAL_ENTITIES];
            
            if (entityContainer == null)
            {
                GameObject containerObj = new GameObject("EntityContainer");
                entityContainer = containerObj.transform;
            }
            
            for (int i = 0; i < PLAYER_COUNT; i++)
            {
                if (i < playerPrefabs.Length && playerPrefabs[i] != null && i < playerSpawnPoints.Length)
                {
                    GameObject playerObj = Instantiate(playerPrefabs[i], playerSpawnPoints[i].position, Quaternion.identity, entityContainer);
                    BattleEntity entity = playerObj.GetComponent<BattleEntity>();
                    
                    if (entity != null)
                    {
                        entity.Initialize();
                        entities[i] = entity;
                    }
                }
            }
            
            if (bossPrefab != null && bossSpawnPoint != null)
            {
                GameObject bossObj = Instantiate(bossPrefab, bossSpawnPoint.position, Quaternion.identity, entityContainer);
                BossEntity boss = bossObj.GetComponent<BossEntity>();
                
                if (boss != null)
                {
                    ConfigureBoss(boss);
                    boss.Initialize();
                    entities[BOSS_INDEX] = boss;
                }
            }
        }
        
        private void ConfigureBoss(BossEntity boss)
        {
            if (BossContainer.Instance != null)
            {
                var container = BossContainer.Instance;
                
                if (!string.IsNullOrEmpty(container.CurrentBossName))
                    boss.EntityName = container.CurrentBossName;
                
                boss.ElementType = container.CurrentBossType;
                
                if (container.CurrentBossImageData != null)
                    SetBossSprite(boss, container.CurrentBossImageData);
            }
            
            if (defaultShieldPattern != null)
            {
                var shieldPatternField = typeof(BossEntity).GetField("_shieldPattern", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (shieldPatternField != null)
                    shieldPatternField.SetValue(boss, defaultShieldPattern);
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
                spriteRenderer.sprite = sprite;
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
            
            if (!hasShieldMove && bossShieldMove != null)
                boss.MoveSet.Add(bossShieldMove);
        }
        
        private void InitializeTurnOrder()
        {
            turnOrder = new List<BattleEntity>();
            
            for (int i = 0; i < PLAYER_COUNT; i++)
            {
                if (entities[i] != null && entities[i].CurrentHP > 0)
                    turnOrder.Add(entities[i]);
            }
            
            if (entities[BOSS_INDEX] != null)
                turnOrder.Add(entities[BOSS_INDEX]);
            
            currentTurnIndex = 0;
            turnCount = 0;
        }
        
        private void TransitionToState(BattleState newState)
        {
            if (currentState == newState) return;
            
            currentState = newState;
            
            switch (newState)
            {
                case BattleState.PlayerChoice:
                    HandlePlayerChoice();
                    break;
                case BattleState.BossAction:
                    StartCoroutine(HandleBossTurn());
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
            var currentEntity = GetCurrentEntity();
            if (currentEntity == null) return;
            
            int playerIndex = GetCurrentPlayerIndex();
            if (playerIndex < 0) return;
            
            cameraManager.SwitchCameraTo((CineCamType)playerIndex);
            SetSpriteAlphaExclusive(playerIndex);
            
            if (currentEntity.IsStunned)
            {
                StartCoroutine(ShowStunnedAndAdvance(currentEntity));
            }
            else if (currentEntity.IsCharging)
            {
                var autoAction = new ActionData(
                    ActionType.Move,
                    currentEntity.ChargingMoveIndex,
                    (EntityType)playerIndex,
                    currentEntity.ChargingMoveTarget
                );
                EnqueueAction(() => ExecuteAction(autoAction));
            }
            else
            {
                uiController.ShowActionMenuForPlayer(playerIndex);
            }
        }
        
        private IEnumerator ShowStunnedAndAdvance(BattleEntity entity)
        {
            entity.StartTurn();
            uiController.ShowMessage($"{entity.EntityName} is stunned!", battleSettings.MessageDuration);
            yield return new WaitForSeconds(battleSettings.MessageDuration);
            AdvanceTurn();
        }
        
        private IEnumerator HandleBossTurn()
        {
            cameraManager.SwitchCameraTo(CineCamType.ZoomOut);
            SetSpriteAlphaExclusive(-1);
            
            yield return new WaitForSeconds(battleSettings.TurnTransitionDelay);
            
            var boss = GetBoss();
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
                var players = GetAlivePlayersArray();
                var bossAI = new UtilityAI(boss, players, aiWeights, turnCount);
                var decision = bossAI.Decide();
                
                if (decision.MoveIndex < 0) yield break;
                
                bossAction = new ActionData(
                    ActionType.Move,
                    decision.MoveIndex,
                    EntityType.Boss,
                    decision.TargetEntity
                );
            }
            
            yield return ExecuteAction(bossAction);
            
            TransitionToState(BattleState.CheckBattleEnd);
        }
        
        public void OnActionSelected(ActionType actionType, int actionIndex, int playerIndex)
        {
            if (actionType == ActionType.Move)
            {
                var entity = entities[playerIndex];
                var moveData = entity.GetMoveData(actionIndex);

                EntityType defaultTarget = EntityType.Boss;
                if (moveData != null && moveData.AllowedTargetSide == TargetSide.Self)
                    defaultTarget = (EntityType)playerIndex;

                pendingAction = new ActionData(
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
                EnqueueAction(() => ExecuteAction(action));
            }
        }
        
        public void OnTargetSelected(EntityType target)
        {
            if (pendingAction.Source == EntityType.Boss) return;
            
            pendingAction.Target = target;
            EnqueueAction(() => ExecuteAction(pendingAction));
        }
        
        private void EnqueueAction(System.Func<IEnumerator> action)
        {
            actionQueue.Enqueue(action);
            
            if (!isProcessingQueue)
                StartCoroutine(ProcessActionQueue());
        }
        
        private IEnumerator ProcessActionQueue()
        {
            isProcessingQueue = true;
            
            while (actionQueue.Count > 0)
            {
                var action = actionQueue.Dequeue();
                yield return StartCoroutine(action());
            }
            
            isProcessingQueue = false;
            
            if (currentState == BattleState.ExecutingAction)
                AdvanceTurn();
        }
        
        private IEnumerator ExecuteAction(ActionData action)
        {
            TransitionToState(BattleState.ExecutingAction);
            
            var source = entities[(int)action.Source];
            
            if (source.IsGuarding)
                source.SetGuardState(false);
            
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
            var source = entities[(int)action.Source];
            var moveInst = source.GetMoveInstance(action.ActionIndex);
            if (moveInst == null) yield break;
            
            if (moveInst.Data.RequiresCharge && !source.IsCharging)
            {
                source.SetChargingState(true, action.ActionIndex, action.Target);
                uiController.ShowMessage($"{source.EntityName} is charging up!", battleSettings.MessageDuration);
                yield return new WaitForSeconds(battleSettings.MessageDuration);
                yield break;
            }
            
            uiController.ShowMessage($"{source.EntityName} used {moveInst.Data.Name}!", battleSettings.MessageDuration);
            yield return new WaitForSeconds(battleSettings.MessageDuration);
            
            yield return ApplyMoveEffects(source, action.Target, moveInst);
            
            if (source.IsCharging)
                source.SetChargingState(false);
            
            moveInst.CooldownLeft = moveInst.Data.Cooldown;
        }
        
        private IEnumerator ExecuteGuard(BattleEntity source)
        {
            source.SetGuardState(true);
            uiController.ShowMessage($"{source.EntityName} takes a defensive stance!", battleSettings.MessageDuration);
            yield return new WaitForSeconds(battleSettings.MessageDuration);
        }
        
        private IEnumerator ExecuteTaunt(BattleEntity source)
        {
            uiController.ShowMessage($"{source.EntityName} taunts the enemy!", battleSettings.MessageDuration);
            yield return new WaitForSeconds(battleSettings.MessageDuration);
        }
        
        private IEnumerator ApplyMoveEffects(BattleEntity source, EntityType targetType, MoveInstance moveInst)
        {
            switch (moveInst.Data.Category)
            {
                case MoveCategory.Single:
                    var target = entities[(int)targetType];
                    yield return ApplyToSingleTarget(source, target, moveInst);
                    break;
                    
                case MoveCategory.AOE:
                    GetAOETargets(source, validTargetIndices);
                    yield return ApplyToMultipleTargets(source, validTargetIndices, moveInst);
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
                for (int i = 0; i < PLAYER_COUNT; i++)
                {
                    if (entities[i] != null && entities[i].CurrentHP > 0)
                        targetIndices.Add(i);
                }
            }
            else
            {
                if (entities[BOSS_INDEX] != null && entities[BOSS_INDEX].CurrentHP > 0)
                    targetIndices.Add(BOSS_INDEX);
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
                var target = entities[idx];
                if (target.CurrentHP <= 0) continue;
                
                foreach (var effect in moveInst.Data.Effects)
                {
                    yield return ApplyEffect(source, target, moveInst, effect);
                }
            }
        }
        
        private IEnumerator ApplyToRandomTargets(BattleEntity source, MoveInstance moveInst)
        {
            int hitCount = Random.Range(battleSettings.MinMultiHits, battleSettings.MaxMultiHits);
            
            for (int i = 0; i < hitCount; i++)
            {
                GetValidRandomTargets(source, validTargetIndices);
                if (validTargetIndices.Count == 0) break;
                
                int randomIndex = Random.Range(0, validTargetIndices.Count);
                var target = entities[validTargetIndices[randomIndex]];
                
                foreach (var effect in moveInst.Data.Effects)
                {
                    if (effect.EffectType == MoveEffectType.Damage)
                        yield return ApplyEffect(source, target, moveInst, effect);
                }
                
                yield return new WaitForSeconds(battleSettings.MultiHitDelay);
            }
        }
        
        private void GetValidRandomTargets(BattleEntity source, List<int> targetIndices)
        {
            targetIndices.Clear();
            
            if (source is BossEntity)
            {
                for (int i = 0; i < PLAYER_COUNT; i++)
                {
                    if (entities[i] != null && entities[i].CurrentHP > 0)
                        targetIndices.Add(i);
                }
            }
            else
            {
                if (entities[BOSS_INDEX] != null && entities[BOSS_INDEX].CurrentHP > 0)
                    targetIndices.Add(BOSS_INDEX);
            }
        }
        
        private IEnumerator ApplyEffect(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect)
        {
            switch (effect.EffectType)
            {
                case MoveEffectType.Damage:
                    yield return ProcessDamage(source, target, moveInst, effect);
                    break;
                case MoveEffectType.Heal:
                    yield return ProcessHeal(source, target, moveInst, effect);
                    break;
                case MoveEffectType.Buff:
                    yield return ProcessBuff(source, target, moveInst, effect);
                    break;
                case MoveEffectType.Debuff:
                    yield return ProcessDebuff(source, target, moveInst, effect);
                    break;
                case MoveEffectType.Shield:
                    yield return ProcessShield(source, target, moveInst, effect);
                    break;
                case MoveEffectType.Stun:
                    yield return ProcessStun(source, target, moveInst, effect);
                    break;
                case MoveEffectType.Stealth:
                    yield return ProcessStealth(source, target, moveInst, effect);
                    break;
                case MoveEffectType.Counter:
                    yield return ProcessCounter(source, target, moveInst, effect);
                    break;
                case MoveEffectType.Taunt:
                    yield return ProcessTaunt(source, target, moveInst, effect);
                    break;
            }
        }
        
        private IEnumerator ProcessDamage(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect)
        {
            var hit = DamageFormula.GetRawHit(source, effect.Power, effect.Accuracy, effect.CritChance);
            
            yield return SpawnEffect(target, moveInst.Data);
            
            if (!hit.IsHit)
            {
                uiController.ShowMessage("Miss!", 1f);
                yield break;
            }
            
            float effectiveness = TypeChart.GetEffectiveness(moveInst.Data.Type, target.ElementType);
            bool isWeakness = effectiveness > 1f;
            
            int prevHP = target.CurrentHP;
            target.TakeDamage(moveInst.Data.Type, hit.Damage);
            
            uiController.UpdateEntityHP(target);
            
            yield return target.PlayDamageFlash(moveInst.Data.Type, battleSettings.MessageDuration);
            
            if (isWeakness && target is BossEntity)
            {
                uiController.ShowMessage("효과가 굉장했다!", 2f);
                yield return new WaitForSeconds(2f);
            }
            
            if (target.CurrentHP <= 0)
                OnEntityDefeated(target);
        }
        
        private IEnumerator ProcessHeal(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect)
        {
            yield return SpawnEffect(target, moveInst.Data);
            
            int prevHP = target.CurrentHP;
            target.CurrentHP = Mathf.Min(target.CurrentHP + effect.Power, target.MaxHP);
            
            uiController.UpdateEntityHP(target);
            
            yield return new WaitForSeconds(0.5f);
        }
        
        private IEnumerator ProcessBuff(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect)
        {
            target.ApplyBuff(effect.BuffsType, effect.Power / 100f);
            uiController.UpdateBuffIcon(target, effect.BuffsType);
            
            yield return SpawnEffect(target, moveInst.Data);
        }
        
        private IEnumerator ProcessDebuff(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect)
        {
            target.ApplyDebuff(effect.BuffsType, effect.Power / 100f);
            uiController.UpdateBuffIcon(target, effect.BuffsType);
            
            yield return SpawnEffect(target, moveInst.Data);
        }
        
        private IEnumerator ProcessShield(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect)
        {
            if (target is BossEntity boss)
            {
                var trigger = boss.GetAvailableShieldTrigger(turnCount);
                
                if (trigger != null)
                {
                    int previousHP = boss.CurrentHP;
                    boss.ActivateShield(trigger);
                    
                    uiController.AnimateShieldConversion(boss, previousHP, boss.CurrentHP, boss.ShieldHP);
                    uiController.ShowMessage($"{boss.EntityName} activates shield!", 1.5f);
                    
                    yield return new WaitForSeconds(0.5f);
                    yield return SpawnEffect(target, moveInst.Data);
                }
            }
        }
        
        private IEnumerator ProcessStun(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect)
        {
            if (DamageFormula.CheckStun(effect.Accuracy))
            {
                target.ApplyStun(battleSettings.ShieldBreakStunDuration);
                yield return SpawnEffect(target, moveInst.Data);
            }
        }
        
        private IEnumerator ProcessStealth(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect)
        {
            target.ApplyStealth(2);
            uiController.ShowMessage($"{target.EntityName} becomes stealthed!", 1.5f);
            yield return new WaitForSeconds(1.5f);
        }
        
        private IEnumerator ProcessCounter(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect)
        {
            target.ApplyStatusEffect(MoveEffectType.Counter, 3);
            uiController.ShowMessage($"{target.EntityName} prepares to counter!", 1.5f);
            yield return new WaitForSeconds(1.5f);
        }
        
        private IEnumerator ProcessTaunt(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect)
        {
            target.ApplyStatusEffect(MoveEffectType.Taunt, 2);
            uiController.ShowMessage($"{target.EntityName} taunts the enemy!", 1.5f);
            yield return new WaitForSeconds(1.5f);
        }
        
        private IEnumerator SpawnEffect(BattleEntity target, MoveData moveData)
        {
            if (moveData.VFXPrefab != null)
            {
                GameObject effectObj = Instantiate(moveData.VFXPrefab, target.transform.position, Quaternion.identity);
                var effectComponent = effectObj.GetComponent<Effects.Effect>();
                
                if (effectComponent == null)
                {
                    effectComponent = effectObj.AddComponent<Effects.Effect>();
                    effectComponent.SetLifetime(battleSettings.EffectDuration);
                }
                
                yield return null;
            }
        }
        
        public void QueueCounterAttack(BattleEntity counter)
        {
            var boss = entities[BOSS_INDEX];
            if (boss.CurrentHP > 0)
            {
                EnqueueAction(() => ExecuteCounterAttack(counter, boss));
            }
        }
        
        private IEnumerator ExecuteCounterAttack(BattleEntity counter, BattleEntity boss)
        {
            uiController.ShowMessage($"{counter.EntityName} counters!", 1f);
            yield return new WaitForSeconds(1f);
            
            int damage = Mathf.RoundToInt(counter.Attack * 0.5f);
            boss.TakeDamage(counter.ElementType, damage);
            
            uiController.UpdateEntityHP(boss);
        }
        
        private void AdvanceTurn()
        {
            if (currentTurnIndex < turnOrder.Count)
                turnOrder[currentTurnIndex].EndTurn();
            
            currentTurnIndex++;
            SkipDeadEntities();
            
            if (currentTurnIndex >= turnOrder.Count)
            {
                if (ShouldGoToBossTurn())
                {
                    TransitionToState(BattleState.BossAction);
                }
                else
                {
                    StartNewRound();
                    TransitionToState(BattleState.PlayerChoice);
                }
            }
            else
            {
                TransitionToState(BattleState.PlayerChoice);
            }
        }
        
        private void SkipDeadEntities()
        {
            while (currentTurnIndex < turnOrder.Count && turnOrder[currentTurnIndex].CurrentHP <= 0)
                currentTurnIndex++;
        }
        
        private void StartNewRound()
        {
            currentTurnIndex = 0;
            turnCount++;
            
            for (int i = turnOrder.Count - 1; i >= 0; i--)
            {
                if (turnOrder[i].CurrentHP <= 0)
                {
                    turnOrder.RemoveAt(i);
                }
            }
        }
        
        private bool ShouldGoToBossTurn()
        {
            for (int i = currentTurnIndex; i < turnOrder.Count; i++)
            {
                if (turnOrder[i] is BossEntity && turnOrder[i].CurrentHP > 0)
                    return true;
            }
            return false;
        }
        
        private void OnEntityDefeated(BattleEntity entity)
        {
            EnqueueAction(() => WaitAndCheckBattleEnd());
        }
        
        private IEnumerator WaitAndCheckBattleEnd()
        {
            yield return new WaitForSeconds(0.5f);
            CheckBattleEnd();
        }
        
        private void CheckBattleEnd()
        {
            bool bossDefeated = entities[BOSS_INDEX].CurrentHP <= 0;
            bool allPlayersDefeated = true;
            
            for (int i = 0; i < PLAYER_COUNT; i++)
            {
                if (entities[i].CurrentHP > 0)
                {
                    allPlayersDefeated = false;
                    break;
                }
            }
            
            if (bossDefeated || allPlayersDefeated)
            {
                TransitionToState(BattleState.BattleEnded);
            }
            else if (currentState == BattleState.CheckBattleEnd)
            {
                StartNewRound();
                TransitionToState(BattleState.PlayerChoice);
            }
        }
        
        private void HandleBattleEnd()
        {
            actionQueue.Clear();
            bool playerWon = entities[BOSS_INDEX].CurrentHP <= 0;
            uiController.OnBattleEnded(playerWon);
        }
        
        private void SetSpriteAlphaExclusive(int activeIndex)
        {
            for (int i = 0; i < PLAYER_COUNT; i++)
            {
                var sr = entities[i].SpriteRenderer;
                if (!sr) continue;
                
                var color = sr.color;
                color.a = (activeIndex < 0 || i == activeIndex) 
                    ? GameConstants.UI.ACTIVE_SPRITE_ALPHA 
                    : GameConstants.UI.INACTIVE_SPRITE_ALPHA;
                sr.color = color;
            }
        }
        
        public BattleEntity GetEntity(EntityType type) => entities[(int)type];
        public BattleEntity GetEntity(int index) => index >= 0 && index < entities.Length ? entities[index] : null;
        public BossEntity GetBoss() => entities[BOSS_INDEX] as BossEntity;
        
        public List<BattleEntity> GetAlivePlayers()
        {
            var alivePlayers = new List<BattleEntity>(PLAYER_COUNT);
            for (int i = 0; i < PLAYER_COUNT; i++)
            {
                if (entities[i] != null && entities[i].CurrentHP > 0)
                    alivePlayers.Add(entities[i]);
            }
            return alivePlayers;
        }
        
        private BattleEntity[] GetAlivePlayersArray()
        {
            var alivePlayers = GetAlivePlayers();
            var players = new BattleEntity[alivePlayers.Count];
            for (int i = 0; i < alivePlayers.Count; i++)
            {
                players[i] = alivePlayers[i];
            }
            return players;
        }
        
        private BattleEntity GetCurrentEntity() => currentTurnIndex < turnOrder.Count ? turnOrder[currentTurnIndex] : null;
        private int GetCurrentPlayerIndex()
        {
            var currentEntity = GetCurrentEntity();
            if (currentEntity is BossEntity) return -1;
            
            for (int i = 0; i < PLAYER_COUNT; i++)
            {
                if (entities[i] == currentEntity)
                    return i;
            }
            return -1;
        }
        
        public int GetCurrentTurn() => turnCount;
        
        private void OnDestroy()
        {
            if (entityContainer != null)
            {
                foreach (Transform child in entityContainer)
                    Destroy(child.gameObject);
            }
        }
    }
}