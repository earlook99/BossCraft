using System;
using System.Collections;
using System.Collections.Generic;
using Entity;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;
using Data;

namespace GameSystem
{
    /// <summary>
    /// Manages the user interface for the battle system.
    /// This includes displaying character statuses, action menus, and battle messages.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BattleManager _battleManager;

        // ---- (Scene-placed) Boss Area, Party Area
        [Header("UI Containers")]
        [SerializeField] private RectTransform _bossArea; // Parent transform for boss status UI
        [SerializeField] private RectTransform _partyArea; // Parent transform for player party status UIs

        // ---- (Scene-placed) Panel (GameObject) + ActionMenuUI attached to it
        [Header("Action Menu")]
        [SerializeField] private GameObject _actionMenuPanel; // The panel GameObject holding the action menu
        [SerializeField] private ActionMenuUI _actionMenuUI; // The script component for the action menu

        [Header("Target Selection")]
        [SerializeField] private TargetSelectionUI _targetSelectionUI; // 타겟 선택 UI

        [Header("Other UI")]
        [SerializeField] private GameObject _battleInfo; // GameObject used to display battle messages. Battle messages, etc.

        // ---- (Reference to Prefab made in Project) ----
        [Header("Prefabs")]
        [SerializeField] private CharacterStatusUI _characterStatusPrefab; // Prefab for character status UI elements

        private Dictionary<int, CharacterStatusUI> _entityStatusUIs = new Dictionary<int, CharacterStatusUI>(); // Cache for instantiated status UIs
        private BattleEntity[] _battleEntities; // Cache for battle entities
        private TextMeshProUGUI _battleMessageTextComponent; // Cached TextMeshProUGUI component for battle messages

        private int _currentPlayerIndex; // Index of the player character whose turn it is to act
        
        // 타겟 선택을 위한 임시 저장
        private ActionType _pendingActionType;
        private int _pendingActionIndex;

        /// <summary>
        /// Called when the script instance is being loaded.
        /// Ensures BattleManager reference and initializes UI components.
        /// </summary>
        private void Awake()
        {
            if (_battleManager == null)
            {
                _battleManager = FindAnyObjectByType<BattleManager>();
            }
            
            // TargetSelectionUI가 없으면 생성
            if (_targetSelectionUI == null)
            {
                var targetSelectionGO = new GameObject("TargetSelectionUI");
                targetSelectionGO.transform.SetParent(transform);
                _targetSelectionUI = targetSelectionGO.AddComponent<TargetSelectionUI>();
            }
            
            // Deactivate action menu initially
            if (_actionMenuPanel != null)
            {
                _actionMenuPanel.SetActive(false);
            }

            if (_battleInfo != null) 
            { 
                _battleMessageTextComponent = _battleInfo.GetComponentInChildren<TextMeshProUGUI>(); 
            }
            
            // Initial setup for battle UI
            SetupBattleUI();
        }

        /// <summary>
        /// Called before the first frame update.
        /// Initializes the current player index.
        /// </summary>
        private void Start()
        {
            _currentPlayerIndex = 0;
            
            // TargetSelectionUI 초기화
            if (_targetSelectionUI != null && _battleEntities != null)
            {
                _targetSelectionUI.Setup(this, _battleEntities);
            }
        }

        /// <summary>
        /// Sets up the entire battle UI, including cleaning up existing elements and creating new ones.
        /// </summary>
        public void SetupBattleUI()
        {
            CleanupExistingUI();

            // Get entity array from BattleManager
            _battleEntities = _battleManager.Entities;
            
            // Create Boss UI
            CreateBossStatusUI();
            // Create Player Party UI
            CreatePartyStatusUIs();
        }

        /// <summary>
        /// Cleans up any existing character status UI elements.
        /// </summary>
        private void CleanupExistingUI()
        {
            foreach (var kv in _entityStatusUIs)
            {
                if (kv.Value != null && kv.Value.gameObject != null)
                {
                    Destroy(kv.Value.gameObject);
                }
            }
            _entityStatusUIs.Clear();
        }

        /// <summary>
        /// Creates and sets up the status UI for the boss entity.
        /// </summary>
        private void CreateBossStatusUI()
        {
            var bossEntity = _battleEntities[(int)EntityType.Boss];
            if (bossEntity == null || _bossArea == null || _characterStatusPrefab == null)
            {
                return;
            }

            // Instantiate Prefab -> Attach to Boss Area
            var statusUI = Instantiate(_characterStatusPrefab, _bossArea);
            statusUI.Setup(bossEntity, (int)EntityType.Boss);

            _entityStatusUIs.Add((int)EntityType.Boss, statusUI);
        }

        /// <summary>
        /// Creates and sets up the status UIs for all player characters in the party.
        /// </summary>
        private void CreatePartyStatusUIs()
        {
            if (_partyArea == null || _characterStatusPrefab == null)
            {
                return;
            }

            // 4 characters (Character1~4)
            for (int i = 0; i < 4; i++)
            {
                var entity = _battleEntities[i];
                if (entity == null) continue;

                var statusUI = Instantiate(_characterStatusPrefab, _partyArea);
                statusUI.Setup(entity, i);

                _entityStatusUIs.Add(i, statusUI);
            }
        }
        
        /// <summary>
        /// Shows the action menu for the specified player.
        /// </summary>
        /// <param name="currentPlayerIndex">The index of the player whose turn it is.</param>
        public void ShowActionMenuForCurrentPlayer(int currentPlayerIndex)
        {
            _currentPlayerIndex = currentPlayerIndex;
            
            if (_actionMenuPanel)
            {
                _actionMenuPanel.SetActive(true);
            }
            
            if (_actionMenuUI)
            {
                var playerEntity = _battleEntities[_currentPlayerIndex];
                
                // turnCount, etc., are examples
                _actionMenuUI.Setup(this, playerEntity, _currentPlayerIndex);
            }
        }

        /// <summary>
        /// Called when an action is selected from the action menu.
        /// Now handles target selection before constructing ActionData.
        /// </summary>
        /// <param name="actionType">The type of action selected.</param>
        /// <param name="actionIndex">The specific index of the action (e.g., move index, item index).</param>
        public void OnActionSelect(ActionType actionType, int actionIndex)
        {
            // 액션 메뉴를 일단 숨김
            if (_actionMenuPanel != null)
            {
                _actionMenuPanel.SetActive(false);
            }
            
            _pendingActionType = actionType;
            _pendingActionIndex = actionIndex;
            
            // Move 타입이면 타겟 선택이 필요할 수 있음
            if (actionType == ActionType.Move)
            {
                var playerEntity = _battleEntities[_currentPlayerIndex];
                var moveData = playerEntity.GetMoveData(actionIndex);
                
                if (moveData != null)
                {
                    // AOE는 타겟 선택 없이 바로 실행
                    if (moveData.Category == MoveCategory.AOE)
                    {
                        CompleteActionWithTarget(EntityType.Boss); // 더미 타겟
                    }
                    else
                    {
                        // 타겟 선택 시작
                        StartTargetSelection(moveData);
                    }
                }
                else
                {
                    Debug.LogError($"Move data not found for index {actionIndex}");
                }
            }
            else
            {
                // Guard, Taunt 등은 타겟 선택 없이 바로 실행
                CompleteActionWithTarget(EntityType.Boss); // 더미 타겟
            }
        }

        /// <summary>
        /// 타겟 선택을 시작합니다
        /// </summary>
        private void StartTargetSelection(MoveData moveData)
        {
            _targetSelectionUI.StartTargetSelection(
                (EntityType)_currentPlayerIndex,
                moveData.AllowedTargetSide,
                moveData.Category,
                OnTargetSelected,
                OnTargetSelectionCancelled
            );
        }

        /// <summary>
        /// 타겟이 선택되었을 때 호출됩니다
        /// </summary>
        private void OnTargetSelected(EntityType target)
        {
            CompleteActionWithTarget(target);
        }

        /// <summary>
        /// 타겟 선택이 취소되었을 때 호출됩니다
        /// </summary>
        private void OnTargetSelectionCancelled()
        {
            // 액션 메뉴로 돌아가기
            ShowActionMenuForCurrentPlayer(_currentPlayerIndex);
        }

        /// <summary>
        /// 선택된 타겟으로 액션을 완료합니다
        /// </summary>
        private void CompleteActionWithTarget(EntityType target)
        {
            var newAction = new ActionData(
                _pendingActionType, 
                _pendingActionIndex, 
                (EntityType)_currentPlayerIndex, 
                target
            );
            _battleManager.ReceivePlayerChoice(newAction);
        }

        /// <summary>
        /// Called when it's the boss's turn, to hide the player action menu.
        /// </summary>
        private void StartBossTurn()
        {
            // Hide player action menu
            if (_actionMenuPanel != null)
            {
                _actionMenuPanel.SetActive(false);
            }
        }

        // ---- Display battle message ----
        /// <summary>
        /// Displays a battle message on the UI for a specified duration.
        /// </summary>
        /// <param name="message">The message to display.</param>
        /// <param name="duration">How long the message should be visible, in seconds.</param>
        /// <returns>An IEnumerator for the coroutine.</returns>
        public IEnumerator ShowBattleMessage(string message, float duration = 2f)
        {
            if (_battleMessageTextComponent != null)
            {
                _battleMessageTextComponent.text = message;
                if (_battleInfo != null) _battleInfo.SetActive(true); // Show the parent GameObject
                yield return new WaitForSeconds(duration);
                // if (_battleInfo != null) _battleInfo.SetActive(false); // Hide after duration
            }
        }
        
        /// <summary>
        /// Animates the HP bar of a specified entity decreasing from a start HP to an end HP.
        /// </summary>
        /// <param name="entityIndex">The index of the entity whose HP bar to animate.</param>
        /// <param name="startHP">The HP value to start the animation from.</param>
        /// <param name="endHP">The HP value to end the animation at.</param>
        /// <returns>An IEnumerator for the coroutine.</returns>
        public IEnumerator AnimateHPBarUpdate(int entityIndex, int startHP, int endHP)
        {
            float duration = 1.0f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                int currentHP = (int)Mathf.Lerp(startHP, endHP, t);
                _entityStatusUIs[entityIndex].UpdateHP(currentHP);
                yield return null;
            }

            _entityStatusUIs[entityIndex].UpdateHP(endHP);
        }
    }
}