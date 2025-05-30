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

        [Header("UI Containers")]
        [SerializeField] private RectTransform _bossArea; // Parent transform for boss status UI
        [SerializeField] private RectTransform _partyArea; // Parent transform for player party status UIs

        [Header("Action Menu")]
        [SerializeField] private GameObject _actionMenuPanel; // The panel GameObject holding the action menu
        [SerializeField] private ActionMenuUI _actionMenuUI; // The script component for the action menu

        [Header("Target Selection")]
        [SerializeField] private TargetSelectionUI _targetSelectionUI; // 타겟 선택 UI

        [Header("Other UI")]
        [SerializeField] private GameObject _battleInfo; // GameObject used to display battle messages. Battle messages, etc.
        [SerializeField] private BattleEndUI _battleEndUI;

        [Header("Prefabs")]
        [SerializeField] private CharacterStatusUI _characterStatusPrefab; // Prefab for character status UI elements

        private Dictionary<int, CharacterStatusUI> _entityStatusUIs = new Dictionary<int, CharacterStatusUI>(); // Cache for instantiated status UIs
        private BattleEntity[] _battleEntities; // Cache for battle entities
        private TextMeshProUGUI _battleMessageTextComponent; // Cached TextMeshProUGUI component for battle messages

        private int _currentPlayerIndex; // Index of the player character whose turn it is to act
        
        private ActionType _pendingActionType;
        private int _pendingActionIndex;
        
        private void Awake()
        {
            if (_battleManager == null)
            {
                _battleManager = FindAnyObjectByType<BattleManager>();
            }
            
            // TargetSelectionUI가 없으면 생성
            if (_targetSelectionUI == null)
            {
                _targetSelectionUI = GetComponentInChildren<TargetSelectionUI>(true);
            
                if (_targetSelectionUI == null)
                {
                    // 자식에 없으면 생성
                    var targetSelectionGO = new GameObject("TargetSelectionUI");
                    targetSelectionGO.transform.SetParent(transform, false);
                
                    // RectTransform 설정
                    var rect = targetSelectionGO.AddComponent<RectTransform>();
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                
                    _targetSelectionUI = targetSelectionGO.AddComponent<TargetSelectionUI>();
                }
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
        
        private void Start()
        {
            _currentPlayerIndex = 0;
            
            // TargetSelectionUI 초기화
            if (_targetSelectionUI != null && _battleEntities != null)
            {
                _targetSelectionUI.Setup(this, _battleEntities);
            }
        }
        
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
        
        private void OnTargetSelected(EntityType target)
        {
            CompleteActionWithTarget(target);
        }
        
        private void OnTargetSelectionCancelled()
        {
            // 액션 메뉴로 돌아가기
            ShowActionMenuForCurrentPlayer(_currentPlayerIndex);
        }
        
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
        
        private void StartBossTurn()
        {
            // Hide player action menu
            if (_actionMenuPanel != null)
            {
                _actionMenuPanel.SetActive(false);
            }
        }
        
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
        
        public void ShowBattleEndScreen(bool isVictory)
        {
            if (_battleEndUI != null)
            {
                _battleEndUI.ShowBattleEnd(isVictory);
            }
        }
    }
}