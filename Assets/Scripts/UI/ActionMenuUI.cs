using System;
using System.Collections.Generic;
using Data;
using UnityEngine;
using GameSystem;
using Entity;
using GameSystem.UI;
using GameSystem.Pooling;
using UnityEngine.EventSystems;

namespace UI
{
    public enum MenuState
    {
        Main,
        Moves,
        Items
    }

    public class ActionMenuUI : MonoBehaviour
    {
        [SerializeField] private Transform _buttonsContainer;
        [SerializeField] private GameObject _buttonPrefab;
        [SerializeField] private int _maxButtonCount = 8;

        private BattleUIController _uiController;
        private BattleEntity _currentEntity;
        private int _playerIndex;
        private MenuState _currentState;
        
        private List<PoolableButton> _activeButtons = new List<PoolableButton>(8);
        private CanvasGroup _containerCanvasGroup;
        private bool _isInitialized = false;

        private const float ACTION_MESSAGE_DURATION = 1f;
        
        private readonly Action[] _mainMenuActions = new Action[4];
        private readonly string[] _mainMenuLabels = { "Fight", "Item", "Guard", "Taunt" };

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_buttonsContainer != null)
            {
                _containerCanvasGroup = _buttonsContainer.GetComponent<CanvasGroup>();
                if (_containerCanvasGroup == null)
                {
                    _containerCanvasGroup = _buttonsContainer.gameObject.AddComponent<CanvasGroup>();
                }
            }
            
            OptimizeButtons();
            
            _mainMenuActions[0] = () => SwitchMenuState(MenuState.Moves);
            _mainMenuActions[1] = () => SwitchMenuState(MenuState.Items);
            _mainMenuActions[2] = () => HandleActionSelect(ActionType.Guard, -1);
            _mainMenuActions[3] = () => HandleActionSelect(ActionType.Taunt, -1);
            
            _isInitialized = true;
        }

        private void OptimizeButtons()
        {
            var canvasOptimizer = CanvasOptimizer.Instance;
            if (canvasOptimizer != null && _buttonsContainer != null)
            {
                canvasOptimizer.OptimizeUIElement(_buttonsContainer.gameObject);
            }
        }

        public void Setup(BattleUIController controller, BattleEntity entity, int playerIndex)
        {
            if (!_isInitialized)
                Initialize();
            
            _uiController = controller;
            _currentEntity = entity;
            _playerIndex = playerIndex;
            _currentState = MenuState.Main;
            
            if (entity == null) return;

            RefreshButtons();
        }

        private void RefreshButtons()
        {
            GameSystem.Events.UIEvents.RaiseShowMessage($"What will {_currentEntity.EntityName} do?", ACTION_MESSAGE_DURATION);
            
            switch (_currentState)
            {
                case MenuState.Main:
                    ShowMainMenu();
                    break;
                case MenuState.Moves:
                    ShowMoveMenu();
                    break;
                case MenuState.Items:
                    break;
            }
        }

        private void ShowMainMenu()
        {
            int buttonCount = _mainMenuLabels.Length;
            PrepareButtons(buttonCount);
            
            for (int i = 0; i < buttonCount; i++)
            {
                if (i < _activeButtons.Count)
                {
                    _activeButtons[i].Setup(_mainMenuLabels[i], _mainMenuActions[i]);
                }
            }
            
            SetContainerActive(true);
        }

        private void ShowMoveMenu()
        {
            ReadOnlySpan<MoveInstance> currentMoves = _currentEntity.MoveInstances;
            int moveCount = currentMoves.Length;
    
            PrepareButtons(moveCount);

            for (int i = 0; i < moveCount; i++)
            {
                if (i < _activeButtons.Count)
                {
                    int index = i;
                    var moveData = currentMoves[i].Data;
                    _activeButtons[i].Setup(
                        moveData.Name, 
                        () => HandleActionSelect(ActionType.Move, index)
                    );
            
                    // Hover 이벤트 추가
                    var eventTrigger = _activeButtons[i].GetComponent<EventTrigger>() 
                                       ?? _activeButtons[i].gameObject.AddComponent<EventTrigger>();
            
                    eventTrigger.triggers.Clear();
            
                    // PointerEnter
                    var enterEntry = new EventTrigger.Entry();
                    enterEntry.eventID = EventTriggerType.PointerEnter;
                    enterEntry.callback.AddListener((_) => ShowMoveDescription(moveData));
                    eventTrigger.triggers.Add(enterEntry);
            
                    // PointerExit
                    var exitEntry = new EventTrigger.Entry();
                    exitEntry.eventID = EventTriggerType.PointerExit;
                    exitEntry.callback.AddListener((_) => HideMoveDescription());
                    eventTrigger.triggers.Add(exitEntry);
                }
            }
    
            SetContainerActive(true);
        }
        
        private void ShowMoveDescription(MoveData moveData)
        {
            string description = $"{moveData.Name}: Power {moveData.Effects[0].Power}, Type: {moveData.Type}\n{moveData.Description}";
            GameSystem.Events.UIEvents.RaiseShowMessage(description, float.MaxValue);
        }

        private void HideMoveDescription()
        {
            var messageManager = FindAnyObjectByType<GameSystem.UI.MessageUIManager>();
            messageManager?.ClearAllMessages();
        }

        private void PrepareButtons(int count)
        {
            var poolManager = UIPoolManager.Instance;
            bool usePooling = poolManager != null;
            
            count = Mathf.Min(count, _maxButtonCount);
            
            while (_activeButtons.Count < count)
            {
                PoolableButton button = null;
                
                if (usePooling)
                {
                    button = poolManager.CreateButton("", null, _buttonsContainer);
                }
                
                if (button == null)
                {
                    var buttonObj = Instantiate(_buttonPrefab, _buttonsContainer);
                    button = buttonObj.GetComponent<PoolableButton>();
                    if (button == null)
                    {
                        button = buttonObj.AddComponent<PoolableButton>();
                    }
                }
                
                _activeButtons.Add(button);
            }
            
            for (int i = count; i < _activeButtons.Count; i++)
            {
                if (_activeButtons[i] != null)
                {
                    if (usePooling)
                    {
                        poolManager.ReturnUI(_activeButtons[i]);
                    }
                    else
                    {
                        _activeButtons[i].gameObject.SetActive(false);
                    }
                }
            }
            
            if (_activeButtons.Count > count)
            {
                _activeButtons.RemoveRange(count, _activeButtons.Count - count);
            }
        }

        private void SetContainerActive(bool active)
        {
            if (_containerCanvasGroup != null)
            {
                _containerCanvasGroup.alpha = active ? 1f : 0f;
                _containerCanvasGroup.interactable = active;
                _containerCanvasGroup.blocksRaycasts = active;
            }
        }

        private void SwitchMenuState(MenuState newState)
        {
            _currentState = newState;
            RefreshButtons();
        }

        private void HandleActionSelect(ActionType actionType, int actionIndex)
        {
            _uiController.OnActionSelect(actionType, actionIndex);
        }

        private void OnDisable()
        {
            ClearButtons();
        }

        private void OnDestroy()
        {
            ClearButtons();
        }

        private void ClearButtons()
        {
            var poolManager = UIPoolManager.Instance;
            
            foreach (var button in _activeButtons)
            {
                if (button != null)
                {
                    if (poolManager != null)
                    {
                        poolManager.ReturnUI(button);
                    }
                    else
                    {
                        Destroy(button.gameObject);
                    }
                }
            }
            
            _activeButtons.Clear();
        }
    }
}