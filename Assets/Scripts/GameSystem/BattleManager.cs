using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Entity;
using AI;
using Data;
using CameraSystem;
using GameSystem.UI;
using UI;
using static GameSystem.GameConstants.Battle;

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
        
        private List<BattleEntity> pendingDeaths = new List<BattleEntity>();
        
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
            
            CreateEntities();
            
            yield return WaitForAllEntitiesReady();
            
            InitializeTurnOrder();
            
            uiController.Initialize(entities, this);
            
            yield return new WaitForSeconds(0.3f);
            
            TransitionToState(BattleState.PlayerChoice);
        }
        
        private IEnumerator WaitForAllEntitiesReady()
        {
            while (true)
            {
                bool allReady = true;
                foreach (var entity in entities)
                {
                    if (entity != null && !entity.IsInitialized)
                    {
                        allReady = false;
                        break;
                    }
                }
                
                if (allReady) break;
                yield return null;
            }
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
                var shieldPatternField = typeof(BossEntity).GetField("shieldPattern", 
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
            yield return uiController.ShowMessageAuto($"{entity.EntityName} is stunned!", 1.0f);
            AdvanceTurn();
        }
        
        private IEnumerator HandleBossTurn()
        {
            cameraManager.SwitchCameraTo(CineCamType.ZoomOut);
            SetSpriteAlphaExclusive(-1);

            yield return new WaitForSeconds(battleSettings.TurnTransitionDelay);

            var boss = GetBoss();
            if (boss == null) yield break;
    
            Debug.Log($"보스 Attack: {boss.Attack}");
            Debug.Log($"보스 Defense: {boss.Defense}");
    
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
                Debug.Log($"살아있는 플레이어 수: {players.Length}");

                var bossAI = new UtilityAI(boss, players, entities, aiWeights, turnCount);
                var decision = bossAI.Decide();

                Debug.Log($"AI 결정 - MoveIndex: {decision.MoveIndex}, Target: {decision.TargetEntity}");

                if (decision.MoveIndex < 0) 
                {
                    Debug.LogError("보스 AI가 스킬을 선택하지 못함!");
                    yield break;
                }
        
                bossAction = new ActionData(
                    ActionType.Move,
                    decision.MoveIndex,
                    EntityType.Boss,
                    decision.TargetEntity
                );
            }
    
            yield return ExecuteAction(bossAction);
    
            if (pendingDeaths.Count > 0)
            {
                foreach (var entity in pendingDeaths)
                {
                    yield return ShowDeathSequence(entity);
                }
                pendingDeaths.Clear();
            }
    
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
                yield return uiController.ShowMessageAuto($"{source.EntityName} is charging up!", 1.5f);
                yield break;
            }
            
            yield return uiController.ShowMessageAuto($"{source.EntityName} used {moveInst.Data.Name}!", 1.0f);
            
            yield return ApplyMoveEffects(source, action.Target, moveInst);
            
            if (source.IsCharging)
                source.SetChargingState(false);
            
            moveInst.CooldownLeft = moveInst.Data.Cooldown;
        }
        
        private IEnumerator ExecuteGuard(BattleEntity source)
        {
            source.SetGuardState(true);
            yield return uiController.ShowMessageAuto($"{source.EntityName} takes a defensive stance!", 1.0f);
        }
        
        private IEnumerator ExecuteTaunt(BattleEntity source)
        {
            yield return uiController.ShowMessageAuto($"{source.EntityName} taunts the enemy!", 1.0f);
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
            
            yield return new WaitForSeconds(0.3f);
            
            if (!hit.IsHit)
            {
                yield return uiController.ShowMessageAndWaitForClick("Miss!");
                yield break;
            }
            
            float effectiveness = TypeChart.GetEffectiveness(moveInst.Data.Type, target.ElementType);
            bool isWeakness = effectiveness > 1f;
            
            // 보스의 실드 상태 저장
            BossEntity boss = target as BossEntity;
            int prevShieldHP = boss?.ShieldHP ?? 0;
            int prevShieldStacks = boss?.ShieldStacks ?? 0;
            bool hadShield = boss?.HasShield ?? false;
            
            int prevHP = target.CurrentHP;
            
            // 1. 먼저 데미지 플래시 (깜빡임)
            yield return target.PlayDamageFlash(moveInst.Data.Type, battleSettings.MessageDuration);
            
            // 2. 실제 데미지 적용
            target.TakeDamage(moveInst.Data.Type, hit.Damage);
            
            // 3. HP/실드 UI 업데이트 및 애니메이션
            if (boss != null && hadShield)
            {
                // 실드 데미지 애니메이션
                yield return AnimateShieldDamage(boss, prevShieldHP, prevShieldStacks);
            }
            else
            {
                // 일반 HP 업데이트
                uiController.UpdateEntityHP(target);
                yield return new WaitForSeconds(0.5f);
            }
            
            // 약점 공격 처리
            if (isWeakness && boss != null)
            {
                yield return uiController.ShowMessageAndWaitForClick("효과가 굉장했다!");
                
                // 약점 공격으로 실드가 파괴된 경우
                if (boss.WasWeaknessHit)
                {
                    if (boss.HasShield)
                    {
                        // 실드 파괴 시 즉시 카메라 줌아웃
                        cameraManager.SwitchCameraTo(CineCamType.ZoomOut);
                        yield return new WaitForSeconds(0.3f); // 카메라 전환 대기
                        
                        // 메시지 표시
                        yield return uiController.ShowMessageAuto($"실드가 부서졌다!", 1.5f);
                    }
                    else if (boss.WasCompletelyDestroyed)
                    {
                        // 실드 완전 파괴 시 즉시 카메라 줌아웃
                        cameraManager.SwitchCameraTo(CineCamType.ZoomOut);
                        yield return new WaitForSeconds(0.3f); // 카메라 전환 대기
                        
                        // 실드 완전 파괴 메시지만
                        yield return ShowShieldDestroyedSequence(boss);
                    }
                }
            }
            // 일반 공격으로 실드 파괴
            else if (boss != null && boss.WasStackBroken)
            {
                if (boss.HasShield)
                {
                    // 실드 파괴 시 즉시 카메라 줌아웃
                    cameraManager.SwitchCameraTo(CineCamType.ZoomOut);
                    yield return new WaitForSeconds(0.3f); // 카메라 전환 대기
                    
                    // 메시지 표시
                    yield return uiController.ShowMessageAuto($"실드가 부서졌다!", 1.5f);
                }
                else if (boss.WasCompletelyDestroyed)
                {
                    // 실드 완전 파괴 시 즉시 카메라 줌아웃
                    cameraManager.SwitchCameraTo(CineCamType.ZoomOut);
                    yield return new WaitForSeconds(0.3f); // 카메라 전환 대기
                    
                    // 실드 완전 파괴 메시지만
                    yield return ShowShieldDestroyedSequence(boss);
                }
            }
            
            // 플래그 리셋
            boss?.ResetDamageFlags();
            
            if (target.CurrentHP <= 0)
            {
                OnEntityDefeated(target);
            }
            else if (boss != null && !boss.HasShield)
            {
                // 보스가 데미지를 받은 후 실드 조건 체크
                var trigger = boss.GetAvailableShieldTrigger(turnCount);
                if (trigger != null)
                {
                    Debug.Log($"[SHIELD] Boss shield will trigger after damage! HP: {boss.CurrentHP}/{boss.MaxHP}");
                    // 실드 발동은 현재 액션이 끝난 후에 처리됨 (AdvanceTurn에서)
                }
            }
        }
        
        private IEnumerator AnimateShieldDamage(BossEntity boss, int prevShieldHP, int prevShieldStacks)
        {
            // 스택이 파괴된 경우
            if (boss.ShieldStacks < prevShieldStacks || boss.ShieldHP == 0)
            {
                // 번쩍이는 효과
                yield return uiController.FlashShieldBar(boss);
                
                // 즉시 UI 업데이트 (애니메이션 없이)
                uiController.UpdateEntityShieldImmediate(boss);
                yield return new WaitForSeconds(0.2f);
            }
            else
            {
                // 일반 데미지인 경우에만 HP 감소 애니메이션
                yield return AnimateShieldHPReduction(boss, prevShieldHP, boss.ShieldHP);
            }
        }
        
        private IEnumerator AnimateShieldHPReduction(BossEntity boss, int fromHP, int toHP)
        {
            // 실드 HP 바가 부드럽게 감소하는 애니메이션
            // CharacterStatusUI가 애니메이션을 처리하도록 위임
            uiController.UpdateEntityShield(boss);
            
            // 애니메이션이 완료될 때까지 대기
            yield return new WaitForSeconds(0.5f);
        }
        
        private IEnumerator ShowShieldStackDestroyAnimation(BossEntity boss, int destroyedStacks)
        {
            // 카메라는 이미 전환되어 있음
            
            // 보스 상태 UI 가져오기
            var bossStatusUI = uiController.GetBossStatusUI();
            if (bossStatusUI == null)
            {
                Debug.LogWarning("[SHIELD ANIMATION] Boss status UI not found!");
                yield break;
            }
            
            // 각 파괴된 스택에 대해 이펙트 재생
            int currentStacks = boss.ShieldStacks;
            int maxStacks = boss.MaxShieldStacks;
            
            for (int i = 0; i < destroyedStacks; i++)
            {
                if (boss.ShieldPattern != null && boss.ShieldPattern.EffectPrefab != null)
                {
                    // UI 위치를 가져와서 월드 좌표로 변환
                    int stackIndex = currentStacks + i;
                    Vector3 uiPosition = bossStatusUI.GetShieldStackWorldPosition(stackIndex, maxStacks);
                    
                    // 파괴 이펙트 생성
                    var effect = Instantiate(boss.ShieldPattern.EffectPrefab, uiPosition, Quaternion.identity);
                    effect.transform.localScale = Vector3.one * 0.1f; // 훨씬 더 작게
                    
                    // Sorting Order로 맨 앞에 표시
                    var spriteRenderer = effect.GetComponentInChildren<SpriteRenderer>();
                    if (spriteRenderer != null)
                    {
                        spriteRenderer.sortingOrder = 1000; // 높은 값으로 설정
                    }
                    
                    // Effects.Effect 컴포넌트 확인/추가 (SpawnEffect와 동일)
                    var effectComponent = effect.GetComponent<Effects.Effect>();
                    if (effectComponent == null)
                    {
                        effectComponent = effect.AddComponent<Effects.Effect>();
                    }
                    
                    // 약간의 랜덤 오프셋 추가 (월드 스케일로 조정)
                    float offsetX = UnityEngine.Random.Range(-0.2f, 0.2f);
                    float offsetY = UnityEngine.Random.Range(-0.1f, 0.1f);
                    effect.transform.position += new Vector3(offsetX, offsetY, 0);
                    
                    // 다음 스택 파괴 전 대기
                    if (i < destroyedStacks - 1)
                        yield return new WaitForSeconds(0.3f);
                }
            }
            
            yield return new WaitForSeconds(0.5f);
        }
        
        private IEnumerator ProcessHeal(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect)
        {
            yield return SpawnEffect(target, moveInst.Data);
            
            int prevHP = target.CurrentHP;
            target.CurrentHP = Mathf.Min(target.CurrentHP + effect.Power, target.MaxHP);
            
            uiController.UpdateEntityHP(target);
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
                    yield return uiController.ShowMessageAndWaitForClick($"{boss.EntityName} activates shield!");
                    
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
                yield return uiController.ShowMessageAndWaitForClick($"{target.EntityName} is stunned!");
            }
        }
        
        private IEnumerator ProcessStealth(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect)
        {
            target.ApplyStealth(2);
            yield return SpawnEffect(target, moveInst.Data);
            yield return uiController.ShowMessageAndWaitForClick($"{target.EntityName} becomes stealthed!");
        }
        
        private IEnumerator ProcessCounter(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect)
        {
            target.ApplyStatusEffect(MoveEffectType.Counter, 3);
            yield return SpawnEffect(target, moveInst.Data);
            yield return uiController.ShowMessageAndWaitForClick($"{target.EntityName} prepares to counter!");
        }
        
        private IEnumerator ProcessTaunt(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect effect)
        {
            target.ApplyStatusEffect(MoveEffectType.Taunt, 2);
            yield return SpawnEffect(target, moveInst.Data);
            yield return uiController.ShowMessageAndWaitForClick($"{target.EntityName} taunts the enemy!");
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
                }
                
                yield return new WaitForSeconds(effectComponent.GetDuration());
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
            yield return uiController.ShowMessageAndWaitForClick($"{counter.EntityName} counters!");
            
            int damage = Mathf.RoundToInt(counter.Attack * 0.5f);
            boss.TakeDamage(counter.ElementType, damage);
            
            uiController.UpdateEntityHP(boss);
        }
        
        private void AdvanceTurn()
        {
            if (currentState == BattleState.ExecutingAction && actionQueue.Count > 0)
            {
                Debug.Log("[TURN] Death sequence in progress, skipping turn advance");
                return;
            }
            
            if (currentTurnIndex < turnOrder.Count)
                turnOrder[currentTurnIndex].EndTurn();
    
            // 플레이어 턴 종료 후 보스 실드 체크
            if (currentTurnIndex < turnOrder.Count && !(turnOrder[currentTurnIndex] is BossEntity))
            {
                if (CheckAndQueueBossShield())
                {
                    // 실드가 발동되면 ExecutingAction 상태로 전환
                    TransitionToState(BattleState.ExecutingAction);
                    return;
                }
            }
    
            currentTurnIndex++;
            SkipDeadEntities();
    
            if (currentTurnIndex < turnOrder.Count && turnOrder[currentTurnIndex] is BossEntity)
            {
                TransitionToState(BattleState.BossAction);
            }
            else if (currentTurnIndex >= turnOrder.Count)
            {
                StartNewRound();
                TransitionToState(BattleState.PlayerChoice);
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
        
        private bool CheckAndQueueBossShield()
        {
            var boss = GetBoss();
            Debug.Log($"[SHIELD QUEUE CHECK] Boss null: {boss == null}, HasShield: {boss?.HasShield ?? false}, HP: {boss?.CurrentHP ?? 0}");
            
            if (boss == null || boss.HasShield || boss.CurrentHP <= 0) 
            {
                Debug.Log($"[SHIELD QUEUE CHECK] Skipping - Boss null: {boss == null}, HasShield: {boss?.HasShield ?? false}, Dead: {boss?.CurrentHP <= 0}");
                return false;
            }
            
            // 현재 턴 수와 HP 비율로 실드 발동 조건 체크
            var trigger = boss.GetAvailableShieldTrigger(turnCount);
            if (trigger == null) 
            {
                Debug.Log("[SHIELD QUEUE CHECK] No available trigger");
                return false;
            }
            
            Debug.Log($"[SHIELD] Boss shield triggered! HP: {boss.CurrentHP}/{boss.MaxHP} ({(float)boss.CurrentHP/boss.MaxHP*100:F1}%)");
            
            // 실드 액션을 큐에 추가 (이제 실드는 스킬이 아니라 특수 행동)
            actionQueue.Clear(); // 기존 큐 클리어
            EnqueueAction(() => ExecuteBossShieldInterrupt(trigger));
            
            return true;
        }
        
        private IEnumerator ExecuteBossShieldInterrupt(ShieldTrigger trigger)
        {
            var boss = GetBoss();
            if (boss == null) yield break;
            
            // 보스 쪽으로 카메라 전환 (ZoomOut 사용)
            cameraManager.SwitchCameraTo(CineCamType.ZoomOut);
            yield return new WaitForSeconds(boss.ShieldPattern.ActivationDelay);
            
            // 실드 발동 메시지 (자동 진행)
            yield return uiController.ShowMessageAuto($"{boss.EntityName}{boss.ShieldPattern.ActivationMessage}", 1.5f);
            
            // 실드 효과 재생
            if (boss.ShieldPattern.EffectPrefab != null)
            {
                var effect = Instantiate(boss.ShieldPattern.EffectPrefab, boss.transform.position, Quaternion.identity);
                Destroy(effect, battleSettings.EffectDuration);
            }
            
            // 실드 활성화 전 HP 저장
            int hpBefore = boss.CurrentHP;
            
            // 실드 활성화
            boss.ActivateShield(trigger);
            
            // 실드 전환 애니메이션 실행
            uiController.AnimateShieldActivation(boss, hpBefore);
            yield return new WaitForSeconds(0.8f); // 애니메이션 대기
            
            yield return new WaitForSeconds(0.5f);
            
            // 다음 턴으로 진행
            AdvanceTurn();
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
            if (currentState == BattleState.PlayerChoice || currentState == BattleState.ExecutingAction)
            {
                EnqueueAction(() => ShowDeathSequence(entity));
            }
            else if (currentState == BattleState.BossAction)
            {
                pendingDeaths.Add(entity);
            }
        }
        
        private IEnumerator ShowDeathSequence(BattleEntity entity)
        {
            int entityIndex = GetEntityIndex(entity);
    
            if (entity is BossEntity)
            {
                Debug.Log($"Boss defeated, switching to ZoomOut camera");
                cameraManager.SwitchCameraTo(CineCamType.ZoomOut);
                yield return new WaitForSeconds(2f);
        
                yield return uiController.ShowMessageAndWaitForClick($"{entity.EntityName}를 물리쳤다!");
                yield return FadeOutEntity(entity);
            }
            else if (entityIndex >= 0 && entityIndex < PLAYER_COUNT)
            {
                Debug.Log($"Player {entityIndex} defeated, switching to camera {(CineCamType)entityIndex}");
                cameraManager.SwitchCameraTo((CineCamType)entityIndex);
                SetSpriteAlphaExclusive(entityIndex);
        
                float waitTime = entityIndex == 0 ? 2.2f : 0.7f;
                yield return new WaitForSeconds(waitTime);
        
                yield return uiController.ShowMessageAndWaitForClick($"{entity.EntityName}이(가) 쓰러졌다!");
                yield return FadeOutEntity(entity);
            }
    
            yield return WaitAndCheckBattleEnd();
        }
        
        private IEnumerator FadeOutEntity(BattleEntity entity)
        {
            if (entity.SpriteRenderer == null) yield break;
    
            Color originalColor = entity.SpriteRenderer.color;
            float fadeTime = 0.5f;
            float elapsed = 0f;
    
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(originalColor.a, 0f, elapsed / fadeTime);
                entity.SpriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
                yield return null;
            }
    
            entity.SpriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);
        }

        private int GetEntityIndex(BattleEntity entity)
        {
            for (int i = 0; i < entities.Length; i++)
            {
                if (entities[i] == entity)
                    return i;
            }
            return -1;
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
                var entity = entities[i];
                if (entity == null) continue;
                
                var sr = entity.SpriteRenderer;
                if (sr == null) continue;
                
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
        
        public void NotifyShieldUpdate(BossEntity boss)
        {
            if (boss != null && uiController != null)
            {
                uiController.UpdateEntityShield(boss);
            }
        }
        
        public void NotifyShieldStackBroken(BossEntity boss, int oldStacks, int newStacks)
        {
            if (boss != null && uiController != null)
            {
                StartCoroutine(ShowShieldStackBrokenEffect(boss, oldStacks, newStacks));
            }
        }
        
        public void NotifyShieldDestroyed(BossEntity boss)
        {
            if (boss != null && uiController != null)
            {
                StartCoroutine(ShowShieldDestroyedEffect(boss));
            }
        }
        
        private IEnumerator ShowShieldStackBrokenEffect(BossEntity boss, int oldStacks, int newStacks)
        {
            // 카메라 전환
            cameraManager.SwitchCameraTo(CineCamType.ZoomOut);
            
            // 실드 파괴 메시지
            yield return uiController.ShowMessageAuto($"실드가 부서졌다!", 1.5f);
            
            // TODO: 실드 파괴 이펙트 재생
            if (boss.ShieldPattern != null && boss.ShieldPattern.EffectPrefab != null)
            {
                var effect = Instantiate(boss.ShieldPattern.EffectPrefab, boss.transform.position, Quaternion.identity);
                effect.transform.localScale = Vector3.one * 0.7f; // 작은 크기로
                Destroy(effect, 1.5f);
            }
            
            yield return new WaitForSeconds(0.5f);
        }
        
        private IEnumerator ShowShieldBreakAnimation(BossEntity boss)
        {
            // 카메라는 이미 전환되어 있음
            
            // 보스 상태 UI 가져오기
            var bossStatusUI = uiController.GetBossStatusUI();
            if (bossStatusUI == null)
            {
                Debug.LogWarning("[SHIELD ANIMATION] Boss status UI not found!");
                yield break;
            }
            
            // 실드 파괴 이펙트 재생 (다중 파편 효과)
            if (boss.ShieldPattern != null && boss.ShieldPattern.EffectPrefab != null)
            {
                // 실드 바의 중앙 위치 가져오기
                Vector3 shieldBarCenter = bossStatusUI.GetShieldBarCenterWorldPosition();
                
                // 메인 파괴 이펙트 생성
                var mainEffect = Instantiate(boss.ShieldPattern.EffectPrefab, shieldBarCenter, Quaternion.identity);
                mainEffect.transform.localScale = Vector3.one * 0.15f; // 훨씬 더 작게
                
                // Sorting Order로 맨 앞에 표시
                var mainSpriteRenderer = mainEffect.GetComponentInChildren<SpriteRenderer>();
                if (mainSpriteRenderer != null)
                {
                    mainSpriteRenderer.sortingOrder = 1000;
                }
                
                // 추가 파편 이펙트들
                int fragmentCount = 6;
                for (int i = 0; i < fragmentCount; i++)
                {
                    float angle = (360f / fragmentCount) * i;
                    float radius = 0.5f; // 월드 스케일로 조정
                    Vector3 offset = new Vector3(
                        Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                        Mathf.Sin(angle * Mathf.Deg2Rad) * radius,
                        0
                    );
                    
                    var fragment = Instantiate(boss.ShieldPattern.EffectPrefab, shieldBarCenter + offset, Quaternion.identity);
                    fragment.transform.localScale = Vector3.one * 0.2f; // 파편은 더 작게
                    
                    // 바깥쪽으로 이동하는 애니메이션
                    StartCoroutine(MoveFragmentUI(fragment, offset * 2f, 1.5f));
                }
                
                // 이펙트 재생 시간 대기
                var effectComponent = mainEffect.GetComponent<Effects.Effect>();
                float mainEffectDuration = effectComponent != null ? effectComponent.GetDuration() : 2.0f;
                
                // 충분한 시간 대기
                yield return new WaitForSeconds(mainEffectDuration);
                
                // 여유 시간을 두고 파괴
                Destroy(mainEffect, 0.5f);
            }
            else
            {
                yield return new WaitForSeconds(0.5f);
            }
        }
        
        private IEnumerator MoveFragmentUI(GameObject fragment, Vector3 targetOffset, float duration)
        {
            Vector3 startPos = fragment.transform.position;
            Vector3 endPos = startPos + targetOffset;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // EaseOut 효과
                float easedT = 1f - Mathf.Pow(1f - t, 2f);
                
                fragment.transform.position = Vector3.Lerp(startPos, endPos, easedT);
                
                // 페이드 아웃
                var spriteRenderer = fragment.GetComponentInChildren<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    Color color = spriteRenderer.color;
                    color.a = 1f - t;
                    spriteRenderer.color = color;
                }
                
                // 크기 감소
                float scale = Mathf.Lerp(1f, 0.3f, t);
                fragment.transform.localScale = fragment.transform.localScale * scale;
                
                yield return null;
            }
            
            Destroy(fragment);
        }
        
        private IEnumerator MoveFragment(GameObject fragment, Vector3 targetOffset, float duration)
        {
            Vector3 startPos = fragment.transform.position;
            Vector3 endPos = startPos + targetOffset;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // EaseOut 효과
                float easedT = 1f - Mathf.Pow(1f - t, 2f);
                
                fragment.transform.position = Vector3.Lerp(startPos, endPos, easedT);
                
                // 페이드 아웃
                var spriteRenderer = fragment.GetComponentInChildren<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    Color color = spriteRenderer.color;
                    color.a = 1f - t;
                    spriteRenderer.color = color;
                }
                
                yield return null;
            }
            
            Destroy(fragment);
        }
        
        private IEnumerator ShowShieldDestroyedSequence(BossEntity boss)
        {
            // 실드 완전 파괴 메시지
            yield return uiController.ShowMessageAuto($"{boss.EntityName}의 실드가 완전히 파괴되었다!", 2.0f);
            
            // 기절 메시지
            yield return uiController.ShowMessageAuto($"{boss.EntityName}는 기절했다!", 1.5f);
            
            yield return new WaitForSeconds(0.3f);
        }
        
        private IEnumerator ShowShieldDestroyedEffect(BossEntity boss)
        {
            // 이제 사용하지 않음 - ShowShieldBreakAnimation과 ShowShieldDestroyedSequence로 분리
            yield break;
        }
        
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