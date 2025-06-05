using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameSystem;
using TMPro;
using Entity;
using Data;
using CameraSystem;
using GameSystem.UI;

namespace UI
{
    public class BattleUIController : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private GameObject actionMenuPanel;
        [SerializeField] private ActionMenuUI actionMenuUI;
        [SerializeField] private TargetSelectionUI targetSelectionUI;
        [SerializeField] private BattleEndUI battleEndUI;
        
        [Header("Canvas References")]
        [SerializeField] private Canvas staticCanvas;
        [SerializeField] private Canvas dynamicCanvas;
        [SerializeField] private Canvas overlayCanvas;
        
        [Header("Status UI")]
        [SerializeField] private RectTransform bossArea;
        [SerializeField] private RectTransform partyArea;
        [SerializeField] private CharacterStatusUI playerStatusPrefab;
        [SerializeField] private CharacterStatusUI bossStatusPrefab;
        
        [Header("Effect Prefabs")]
        [SerializeField] private GameObject statusIconPrefab;
        [SerializeField] private GameObject damageTextPrefab;
        
        [Header("Message UI")]
        [SerializeField] private GameObject battleMessagePanel;
        [SerializeField] private TextMeshProUGUI battleMessageText;
        
        [Header("Buff Icons")]
        [SerializeField] private Sprite attackBuffIcon;
        [SerializeField] private Sprite defenseBuffIcon;
        
        private GameSystem.BattleManager battleManager;
        private BattleEntity[] battleEntities;
        private Dictionary<int, CharacterStatusUI> entityStatusUIs = new Dictionary<int, CharacterStatusUI>(5);
        private Dictionary<(int entityIndex, BuffsType buffType), StatusIcon> buffIcons = new Dictionary<(int, BuffsType), StatusIcon>();
        
        private int currentPlayerIndex;
        private ActionType pendingActionType;
        private int pendingActionIndex;
        
        private CanvasGroup actionMenuCanvasGroup;
        private CanvasGroup messageCanvasGroup;
        private Coroutine messageCoroutine;
        
        private const int PLAYER_COUNT = 4;
        private const int BOSS_INDEX = 4;
        
        private void Awake()
        {
            InitializeComponents();
            SetupCanvases();
        }
        
        private void InitializeComponents()
        {
            if (actionMenuPanel != null)
            {
                actionMenuCanvasGroup = actionMenuPanel.GetComponent<CanvasGroup>();
                if (actionMenuCanvasGroup == null)
                    actionMenuCanvasGroup = actionMenuPanel.AddComponent<CanvasGroup>();
            }
            
            if (battleMessagePanel != null)
            {
                messageCanvasGroup = battleMessagePanel.GetComponent<CanvasGroup>();
                if (messageCanvasGroup == null)
                    messageCanvasGroup = battleMessagePanel.AddComponent<CanvasGroup>();
            }
            
            if (targetSelectionUI == null)
                targetSelectionUI = GetComponentInChildren<TargetSelectionUI>(true);
                
            if (targetSelectionUI == null)
                CreateTargetSelectionUI();
        }
        
        private void SetupCanvases()
        {
            if (actionMenuPanel != null && dynamicCanvas != null)
                actionMenuPanel.transform.SetParent(dynamicCanvas.transform, false);
                
            if (targetSelectionUI != null && overlayCanvas != null)
                targetSelectionUI.transform.SetParent(overlayCanvas.transform, false);
                
            if (battleEndUI != null && overlayCanvas != null)
                battleEndUI.transform.SetParent(overlayCanvas.transform, false);
                
            if (battleMessagePanel != null && overlayCanvas != null)
                battleMessagePanel.transform.SetParent(overlayCanvas.transform, false);
                
            if (bossArea != null && dynamicCanvas != null)
                bossArea.SetParent(dynamicCanvas.transform, false);
                
            if (partyArea != null && dynamicCanvas != null)
                partyArea.SetParent(dynamicCanvas.transform, false);
        }
        
        private void CreateTargetSelectionUI()
        {
            var targetSelectionGO = new GameObject("TargetSelectionUI");
            targetSelectionGO.transform.SetParent(transform, false);
            
            var rect = targetSelectionGO.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            
            targetSelectionUI = targetSelectionGO.AddComponent<TargetSelectionUI>();
        }
        
        public void Initialize(BattleEntity[] entities, GameSystem.BattleManager manager)
        {
            battleEntities = entities;
            battleManager = manager;
            
            SetupBattleUI();
            
            if (targetSelectionUI != null)
                targetSelectionUI.Setup(this, battleEntities);
                
            SetActionMenuVisible(false);
        }
        
        private void SetupBattleUI()
        {
            CleanupExistingUI();
            CreateBossStatusUI();
            CreatePartyStatusUIs();
        }
        
        private void CleanupExistingUI()
        {
            foreach (var kvp in entityStatusUIs)
            {
                if (kvp.Value != null && kvp.Value.gameObject != null)
                    Destroy(kvp.Value.gameObject);
            }
            entityStatusUIs.Clear();
            
            foreach (var kvp in buffIcons)
            {
                if (kvp.Value != null && kvp.Value.gameObject != null)
                    Destroy(kvp.Value.gameObject);
            }
            buffIcons.Clear();
        }
        
        private void CreateBossStatusUI()
        {
            if (battleEntities == null || battleEntities.Length < 5) return;
            
            var bossEntity = battleEntities[BOSS_INDEX];
            if (bossEntity == null || bossArea == null || bossStatusPrefab == null)
                return;
                
            var statusUI = Instantiate(bossStatusPrefab, bossArea);
            statusUI.Setup(bossEntity, BOSS_INDEX);
            entityStatusUIs.Add(BOSS_INDEX, statusUI);
        }
        
        private void CreatePartyStatusUIs()
        {
            if (battleEntities == null || partyArea == null || playerStatusPrefab == null)
                return;
                
            for (int i = 0; i < PLAYER_COUNT; i++)
            {
                if (i >= battleEntities.Length) break;
                
                var entity = battleEntities[i];
                if (entity == null) continue;
                
                var statusUI = Instantiate(playerStatusPrefab, partyArea);
                statusUI.Setup(entity, i);
                entityStatusUIs.Add(i, statusUI);
            }
        }
        
        public void OnBattleStarted(BattleEntity[] entities)
        {
            battleEntities = entities;
            SetupBattleUI();
        }
        
        public void OnBattleEnded(bool playerWon)
        {
            if (battleEndUI != null)
                battleEndUI.ShowBattleEnd(playerWon);
        }
        
        public void ShowActionMenuForPlayer(int playerIndex)
        {
            currentPlayerIndex = playerIndex;
            
            if (battleEntities == null || playerIndex >= battleEntities.Length)
                return;
                
            var playerEntity = battleEntities[playerIndex];
            if (playerEntity == null)
                return;
            
            SetActionMenuVisible(true);
                
            if (actionMenuUI)
                actionMenuUI.Setup(this, playerEntity, playerIndex);
        }
        
        private void SetActionMenuVisible(bool visible)
        {
            if (actionMenuCanvasGroup != null)
            {
                actionMenuCanvasGroup.alpha = visible ? 1f : 0f;
                actionMenuCanvasGroup.interactable = visible;
                actionMenuCanvasGroup.blocksRaycasts = visible;
            }
            else if (actionMenuPanel != null)
            {
                actionMenuPanel.SetActive(visible);
            }
        }
        
        public void OnActionSelect(ActionType actionType, int actionIndex)
        {
            SetActionMenuVisible(false);
            
            pendingActionType = actionType;
            pendingActionIndex = actionIndex;
            
            if (actionType == ActionType.Move)
            {
                HandleMoveAction(actionIndex);
            }
            else
            {
                if (battleManager != null)
                    battleManager.OnActionSelected(actionType, actionIndex, currentPlayerIndex);
            }
        }
        
        private void HandleMoveAction(int actionIndex)
        {
            var playerEntity = battleEntities[currentPlayerIndex];
            var moveData = playerEntity.GetMoveData(actionIndex);
            
            if (moveData != null)
            {
                if (moveData.AllowedTargetSide == TargetSide.Ally || 
                    moveData.AllowedTargetSide == TargetSide.Allies)
                {
                    var cameraManager = FindAnyObjectByType<CameraManager>();
                    cameraManager?.SwitchCameraTo(CineCamType.ZoomOut);
                }
                
                if (moveData.AllowedTargetSide == TargetSide.Self)
                {
                    CompleteActionWithTarget((GameSystem.EntityType)currentPlayerIndex);
                }
                else if (moveData.Category == MoveCategory.AOE)
                {
                    CompleteActionWithTarget(GameSystem.EntityType.Boss);
                }
                else
                {
                    StartTargetSelection(moveData);
                }
            }
        }
        
        private void StartTargetSelection(MoveData moveData)
        {
            targetSelectionUI.StartTargetSelection(
                (GameSystem.EntityType)currentPlayerIndex,
                moveData.AllowedTargetSide,
                moveData.Category,
                OnTargetSelected,
                OnTargetSelectionCancelled
            );
        }
        
        private void OnTargetSelected(GameSystem.EntityType target)
        {
            CompleteActionWithTarget(target);
        }
        
        private void OnTargetSelectionCancelled()
        {
            ShowActionMenuForPlayer(currentPlayerIndex);
        }
        
        private void CompleteActionWithTarget(GameSystem.EntityType target)
        {
            if (battleManager != null)
            {
                battleManager.OnActionSelected(pendingActionType, pendingActionIndex, currentPlayerIndex);
                battleManager.OnTargetSelected(target);
            }
        }
        
        public void OnMenuStateChanged(MenuState newState, int playerIndex)
        {
            currentPlayerIndex = playerIndex;
        }
        
        public void ShowMessage(string message, float duration = 2f)
        {
            if (string.IsNullOrEmpty(message)) return;
            
            if (battleMessageText != null)
                battleMessageText.text = message;
                
            if (messageCoroutine != null)
                StopCoroutine(messageCoroutine);
                
            messageCoroutine = StartCoroutine(ShowMessageCoroutine(duration));
        }
        
        private IEnumerator ShowMessageCoroutine(float duration)
        {
            if (messageCanvasGroup != null)
            {
                messageCanvasGroup.alpha = 1f;
                yield return new WaitForSeconds(duration);
                messageCanvasGroup.alpha = 0f;
            }
            else if (battleMessagePanel != null)
            {
                battleMessagePanel.SetActive(true);
                yield return new WaitForSeconds(duration);
                battleMessagePanel.SetActive(false);
            }
        }
        
        public void UpdateEntityHP(BattleEntity entity)
        {
            if (entity == null) return;
            
            int entityIndex = System.Array.IndexOf(battleEntities, entity);
            if (entityIndex >= 0 && entityStatusUIs.TryGetValue(entityIndex, out var statusUI))
            {
                statusUI.UpdateHP(entity.CurrentHP);
            }
        }
        
        public void AnimateShieldConversion(BossEntity boss, int previousHP, int currentHP, int shieldHP)
        {
            if (entityStatusUIs.TryGetValue(BOSS_INDEX, out var statusUI))
            {
                statusUI.AnimateShieldConversion(previousHP, currentHP, shieldHP);
            }
        }
        
        public void ShowDamageText(BattleEntity target, int damage)
        {
            if (damageTextPrefab == null || dynamicCanvas == null) return;
            
            GameObject damageTextObj = Instantiate(damageTextPrefab, dynamicCanvas.transform);
            var damageText = damageTextObj.GetComponent<GameSystem.UI.DamageText>();
            
            if (damageText != null)
            {
                damageText.Setup(target.transform.position, damage, Color.red);
            }
        }
        
        public void UpdateBuffIcon(BattleEntity entity, BuffsType buffType)
        {
            int entityIndex = System.Array.IndexOf(battleEntities, entity);
            if (entityIndex < 0) return;
            
            var key = (entityIndex, buffType);
            int stackCount = buffType == BuffsType.Attack ? entity.AttackBuffStacks : entity.DefenseBuffStacks;
            
            if (stackCount == 0)
            {
                if (buffIcons.TryGetValue(key, out var icon))
                {
                    Destroy(icon.gameObject);
                    buffIcons.Remove(key);
                }
            }
            else
            {
                if (buffIcons.TryGetValue(key, out var icon))
                {
                    icon.UpdateStack(stackCount);
                }
                else
                {
                    CreateBuffIcon(entityIndex, buffType, stackCount);
                }
            }
        }
        
        private void CreateBuffIcon(int entityIndex, BuffsType buffType, int stackCount)
        {
            if (!entityStatusUIs.TryGetValue(entityIndex, out var statusUI)) return;
            if (statusIconPrefab == null) return;
            
            GameObject iconObj = Instantiate(statusIconPrefab, statusUI.transform);
            var icon = iconObj.GetComponent<GameSystem.UI.StatusIcon>();
            
            if (icon == null)
                icon = iconObj.AddComponent<GameSystem.UI.StatusIcon>();
            
            Sprite sprite = buffType == BuffsType.Attack ? attackBuffIcon : defenseBuffIcon;
            icon.Setup(sprite, stackCount);
            
            buffIcons[(entityIndex, buffType)] = icon;
        }
        
        private void OnDestroy()
        {
            if (messageCoroutine != null)
                StopCoroutine(messageCoroutine);
        }
    }
}