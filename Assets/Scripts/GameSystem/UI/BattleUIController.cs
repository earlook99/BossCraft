using CameraSystem;
using Data;
using UnityEngine;
using Entity;
using UI;

namespace GameSystem.UI
{
    public class BattleUIController : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private GameObject _actionMenuPanel;
        [SerializeField] private ActionMenuUI _actionMenuUI;
        [SerializeField] private TargetSelectionUI _targetSelectionUI;
        [SerializeField] private BattleEndUI _battleEndUI;
        
        [Header("Canvas Assignment")]
        [SerializeField] private CanvasType _actionMenuCanvasType = CanvasType.Dynamic;
        [SerializeField] private CanvasType _targetSelectionCanvasType = CanvasType.Overlay;
        [SerializeField] private CanvasType _battleEndCanvasType = CanvasType.Overlay;
        
        [Header("Battle Entities")]
        [SerializeField] private BattleEntity[] _battleEntities = new BattleEntity[5];
        
        [Header("Manager References")]
        [SerializeField] private BattleManager _battleManager;
        
        private StatusUIManager _statusUIManager;
        private MessageUIManager _messageUIManager;
        private CanvasOptimizer _canvasOptimizer;
        
        private int _currentPlayerIndex;
        private ActionType _pendingActionType;
        private int _pendingActionIndex;
        
        private CanvasGroup _actionMenuCanvasGroup;
        private bool _isActionMenuVisible = false;
        
        private void Awake()
        {
            InitializeComponents();
            SetupCanvases();
            OptimizeUIElements();
        }
        
        private void InitializeComponents()
        {
            _statusUIManager = GetComponent<StatusUIManager>();
            if (_statusUIManager == null)
                _statusUIManager = gameObject.AddComponent<StatusUIManager>();
                
            _messageUIManager = GetComponent<MessageUIManager>();
            if (_messageUIManager == null)
                _messageUIManager = gameObject.AddComponent<MessageUIManager>();
            
            if (_targetSelectionUI == null)
                _targetSelectionUI = GetComponentInChildren<TargetSelectionUI>(true);
                
            if (_targetSelectionUI == null)
                CreateTargetSelectionUI();
            
            _canvasOptimizer = CanvasOptimizer.Instance;
            
            if (_actionMenuPanel != null)
            {
                _actionMenuCanvasGroup = _actionMenuPanel.GetComponent<CanvasGroup>();
                if (_actionMenuCanvasGroup == null)
                    _actionMenuCanvasGroup = _actionMenuPanel.AddComponent<CanvasGroup>();
                
                SetActionMenuVisible(false);
            }
            
            if (_battleManager == null)
                _battleManager = FindAnyObjectByType<BattleManager>();
        }
        
        private void SetupCanvases()
        {
            if (_canvasOptimizer == null) return;
            
            if (_actionMenuPanel != null)
                _canvasOptimizer.MoveToCanvas(_actionMenuPanel, _actionMenuCanvasType);
            
            if (_targetSelectionUI != null)
                _canvasOptimizer.MoveToCanvas(_targetSelectionUI.gameObject, _targetSelectionCanvasType);
            
            if (_battleEndUI != null)
                _canvasOptimizer.MoveToCanvas(_battleEndUI.gameObject, _battleEndCanvasType);
        }
        
        private void OptimizeUIElements()
        {
            if (_canvasOptimizer == null) return;
            
            if (_actionMenuPanel != null)
                _canvasOptimizer.OptimizeUIElement(_actionMenuPanel);
            
            if (_targetSelectionUI != null)
                _canvasOptimizer.OptimizeUIElement(_targetSelectionUI.gameObject);
            
            if (_battleEndUI != null)
                _canvasOptimizer.OptimizeUIElement(_battleEndUI.gameObject);
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
            
            _targetSelectionUI = targetSelectionGO.AddComponent<TargetSelectionUI>();
        }
        
        public void OnBattleStarted(BattleEntity[] entities)
        {
            _battleEntities = entities;
            
            _statusUIManager.Initialize(_battleEntities);
            
            if (_targetSelectionUI != null)
                _targetSelectionUI.Setup(this, _battleEntities);
        }
        
        public void OnBattleEnded(bool playerWon)
        {
            if (_battleEndUI != null)
                _battleEndUI.ShowBattleEnd(playerWon);
        }
        
        public void ShowActionMenuForPlayer(int playerIndex)
        {
            _currentPlayerIndex = playerIndex;
            
            if (_battleEntities == null || playerIndex >= _battleEntities.Length)
                return;
                
            var playerEntity = _battleEntities[playerIndex];
            if (playerEntity == null)
                return;
            
            SetActionMenuVisible(true);
                
            if (_actionMenuUI)
                _actionMenuUI.Setup(this, playerEntity, playerIndex);
        }
        
        private void SetActionMenuVisible(bool visible)
        {
            if (_actionMenuCanvasGroup != null)
            {
                _isActionMenuVisible = visible;
                _actionMenuCanvasGroup.alpha = visible ? 1f : 0f;
                _actionMenuCanvasGroup.interactable = visible;
                _actionMenuCanvasGroup.blocksRaycasts = visible;
            }
            else if (_actionMenuPanel != null)
            {
                _actionMenuPanel.SetActive(visible);
            }
        }
        
        public void OnActionSelect(ActionType actionType, int actionIndex)
        {
            SetActionMenuVisible(false);
    
            _pendingActionType = actionType;
            _pendingActionIndex = actionIndex;
    
            if (actionType == ActionType.Move)
            {
                HandleMoveAction(actionIndex);
            }
            else
            {
                if (_battleManager != null)
                    _battleManager.OnActionSelected(actionType, actionIndex, _currentPlayerIndex);
            }
        }
        
        private void HandleMoveAction(int actionIndex)
        {
            var playerEntity = _battleEntities[_currentPlayerIndex];
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
                    CompleteActionWithTarget((EntityType)_currentPlayerIndex);
                }
                else if (moveData.Category == MoveCategory.AOE)
                {
                    CompleteActionWithTarget(EntityType.Boss);
                }
                else
                {
                    StartTargetSelection(moveData);
                }
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
            ShowActionMenuForPlayer(_currentPlayerIndex);
        }
        
        private void CompleteActionWithTarget(EntityType target)
        {
            if (_battleManager != null)
            {
                _battleManager.OnActionSelected(_pendingActionType, _pendingActionIndex, _currentPlayerIndex);
                _battleManager.OnTargetSelected(target);
            }
        }
        
        public void OnMenuStateChanged(MenuState newState, int playerIndex)
        {
            _currentPlayerIndex = playerIndex;
        }
        
        public StatusUIManager GetStatusUIManager() => _statusUIManager;
        public MessageUIManager GetMessageUIManager() => _messageUIManager;
    }
}