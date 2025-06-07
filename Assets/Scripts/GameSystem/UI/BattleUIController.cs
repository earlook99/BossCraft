using System.Collections;
using System.Collections.Generic;
using CameraSystem;
using Data;
using Entity;
using TMPro;
using UI;
using UnityEngine;
using static GameSystem.GameConstants.Battle;

namespace GameSystem.UI
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
        
        [Header("Message UI")]
        [SerializeField] private GameObject battleMessagePanel;
        [SerializeField] private TextMeshProUGUI battleMessageText;
        [SerializeField] private GameObject clickPrompt;
        [SerializeField] private TextMeshProUGUI clickPromptText;
        
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
        
        private static readonly Color COLOR_RED = Color.red;
        private static readonly Color COLOR_WHITE = Color.white;
        
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
                    
                messageCanvasGroup.alpha = 1f;
                
                // clickPromptText가 없으면 동적으로 생성
                if (clickPromptText == null && battleMessageText != null)
                {
                    // battleMessageText의 overflow 설정 확인
                    battleMessageText.overflowMode = TextOverflowModes.Overflow;
                    CreateClickPromptText();
                }
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
        
        private void CreateClickPromptText()
        {
            // clickPromptText 동적 생성
            GameObject promptGO = new GameObject("ClickPromptText");
            promptGO.transform.SetParent(battleMessageText.transform.parent, false);  // battleMessagePanel의 자식으로
            
            // RectTransform 설정
            RectTransform promptRect = promptGO.AddComponent<RectTransform>();
            
            // battleMessageText와 같은 앵커 설정 (Top-Left)
            promptRect.anchorMin = new Vector2(0, 1);
            promptRect.anchorMax = new Vector2(0, 1);
            promptRect.pivot = new Vector2(0, 0.5f);  // 왼쪽 중앙
            
            // 초기 위치는 ShowMessageAndWaitForClick에서 설정
            promptRect.anchoredPosition = Vector2.zero;
            promptRect.sizeDelta = new Vector2(30, 30);
            
            // TextMeshProUGUI 컴포넌트 추가 및 설정
            clickPromptText = promptGO.AddComponent<TextMeshProUGUI>();
            clickPromptText.text = "▼";
            clickPromptText.fontSize = battleMessageText.fontSize;
            clickPromptText.font = battleMessageText.font;
            clickPromptText.color = battleMessageText.color;
            clickPromptText.alignment = TextAlignmentOptions.Center;
            clickPromptText.overflowMode = TextOverflowModes.Overflow;  // 부모 영역 밖으로도 표시
            
            promptGO.SetActive(false);
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
            
            if (!visible && actionMenuUI != null)
            {
                actionMenuUI.ClearAllButtons();
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
        
        public void ShowMessage(string message)
        {
            if (battleMessageText != null)
                battleMessageText.text = message;
                
            if (messageCanvasGroup != null)
            {
                messageCanvasGroup.alpha = 1f;
            }
            else if (battleMessagePanel != null)
            {
                battleMessagePanel.SetActive(true);
            }
            
            if (clickPrompt != null)
                clickPrompt.SetActive(false);
        }
        
        public IEnumerator ShowMessageAuto(string message, float duration = 1.0f)
        {
            ShowMessage(message);
            yield return new WaitForSeconds(duration);
        }
        
        public IEnumerator ShowMessageAndWaitForClick(string message)
        {
            ShowMessage(message);
            
            // 별도의 프롬프트 텍스트 표시
            if (clickPromptText != null)
            {
                // 실제 텍스트 너비 계산
                battleMessageText.ForceMeshUpdate();
                float textWidth = battleMessageText.preferredWidth;
                
                // battleMessageText의 위치 정보
                // Pivot이 (0.5, 0.5)이므로 왼쪽 끝은 X - (Width/2)
                float textLeftX = 633f - (1158f / 2f);  // 54
                float textTopY = -78.7f + (105.5f / 2f);  // -25.95
                
                // 텍스트의 실제 끝 위치
                float promptX = textLeftX + textWidth + 10f;
                float promptY = textTopY - (battleMessageText.fontSize * 0.5f);  // 첫 줄 중앙
                
                RectTransform promptRect = clickPromptText.GetComponent<RectTransform>();
                promptRect.anchoredPosition = new Vector2(promptX, promptY);
                
                clickPromptText.text = "▼";
                clickPromptText.gameObject.SetActive(true);
            }
            else if (clickPrompt != null)
            {
                clickPrompt.SetActive(true);
            }
            
            yield return new WaitForSeconds(0.2f);
            
            // 깜빡임 애니메이션
            float blinkTimer = 0f;
            bool isVisible = true;
            
            while (!Input.GetMouseButtonDown(0))
            {
                blinkTimer += Time.deltaTime;
                
                // 0.5초마다 깜빡임
                if (blinkTimer >= 0.5f)
                {
                    isVisible = !isVisible;
                    blinkTimer = 0f;
                    
                    if (clickPromptText != null)
                    {
                        clickPromptText.gameObject.SetActive(isVisible);
                    }
                    else if (clickPrompt != null)
                    {
                        clickPrompt.SetActive(isVisible);
                    }
                }
                
                yield return null;
            }
            
            // 프롬프트 숨기기
            if (clickPromptText != null)
                clickPromptText.gameObject.SetActive(false);
            else if (clickPrompt != null)
                clickPrompt.SetActive(false);
        }
        
        public void UpdateEntityHP(BattleEntity entity)
        {
            if (entity == null) return;
            
            int entityIndex = GetEntityIndex(entity);
            if (entityIndex >= 0 && entityStatusUIs.TryGetValue(entityIndex, out var statusUI))
            {
                statusUI.UpdateHP(entity.CurrentHP);
            }
        }
        
        public void UpdateEntityShield(BossEntity boss)
        {
            if (boss == null) return;
            
            if (entityStatusUIs.TryGetValue(BOSS_INDEX, out var statusUI))
            {
                statusUI.UpdateShield(boss.ShieldStacks, boss.MaxShieldStacks, boss.ShieldHP);
            }
        }
        
        private int GetEntityIndex(BattleEntity entity)
        {
            for (int i = 0; i < battleEntities.Length; i++)
            {
                if (battleEntities[i] == entity)
                    return i;
            }
            return -1;
        }
        
        public void AnimateShieldConversion(BossEntity boss, int previousHP, int currentHP, int shieldHP)
        {
            if (entityStatusUIs.TryGetValue(BOSS_INDEX, out var statusUI))
            {
                statusUI.AnimateShieldConversion(previousHP, currentHP, shieldHP);
            }
        }
        
        public void AnimateShieldActivation(BossEntity boss, int hpBefore)
        {
            if (entityStatusUIs.TryGetValue(BOSS_INDEX, out var statusUI))
            {
                statusUI.AnimateShieldConversion(hpBefore, boss.CurrentHP, boss.ShieldHP);
            }
        }
        
        public void UpdateBuffIcon(BattleEntity entity, BuffsType buffType)
        {
            int entityIndex = GetEntityIndex(entity);
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
        
        public CharacterStatusUI GetBossStatusUI()
        {
            return entityStatusUIs.TryGetValue(BOSS_INDEX, out var statusUI) ? statusUI : null;
        }
        
        public IEnumerator FlashShieldBar(BossEntity boss)
        {
            if (entityStatusUIs.TryGetValue(BOSS_INDEX, out var statusUI))
            {
                yield return statusUI.FlashShieldBar();
            }
            else
            {
                yield return null;
            }
        }
        
        public void UpdateEntityShieldImmediate(BossEntity boss)
        {
            if (entityStatusUIs.TryGetValue(BOSS_INDEX, out var statusUI))
            {
                statusUI.UpdateShieldImmediate(boss.ShieldStacks, boss.MaxShieldStacks, boss.ShieldHP);
            }
        }
        
        private void OnDestroy()
        {
        }
    }
}