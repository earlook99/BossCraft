using System;
using System.Collections.Generic;
using Data;
using UnityEngine;
using GameSystem;
using Entity;
using GameSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

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
        
        private List<GameObject> _buttons = new List<GameObject>(8);
        private CanvasGroup _containerCanvasGroup;
        private bool _isInitialized = false;
        
        private MessageUIManager _messageUIManager;

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
            
            CreateButtons();
            OptimizeButtons();
            
            _mainMenuActions[0] = () => SwitchMenuState(MenuState.Moves);
            _mainMenuActions[1] = () => SwitchMenuState(MenuState.Items);
            _mainMenuActions[2] = () => HandleActionSelect(ActionType.Guard, -1);
            _mainMenuActions[3] = () => HandleActionSelect(ActionType.Taunt, -1);
            
            _isInitialized = true;
            
            _messageUIManager = FindAnyObjectByType<MessageUIManager>();
        }

        private void CreateButtons()
        {
            for (int i = 0; i < _maxButtonCount; i++)
            {
                GameObject buttonObj = Instantiate(_buttonPrefab, _buttonsContainer);
                buttonObj.SetActive(false);
                _buttons.Add(buttonObj);
            }
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
            if (_messageUIManager != null)
                _messageUIManager.ShowMessage($"What will {_currentEntity.EntityName} do?", ACTION_MESSAGE_DURATION);
            
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
            HideAllButtons();
            
            for (int i = 0; i < _mainMenuLabels.Length && i < _buttons.Count; i++)
            {
                SetupButton(_buttons[i], _mainMenuLabels[i], _mainMenuActions[i]);
                _buttons[i].SetActive(true);
            }
            
            SetContainerActive(true);
        }

        private void ShowMoveMenu()
        {
            HideAllButtons();
            
            ReadOnlySpan<MoveInstance> currentMoves = _currentEntity.MoveInstances;
            int moveCount = currentMoves.Length;

            for (int i = 0; i < moveCount && i < _buttons.Count; i++)
            {
                int index = i;
                var moveData = currentMoves[i].Data;
                
                SetupButton(_buttons[i], moveData.Name, () => HandleActionSelect(ActionType.Move, index));
                _buttons[i].SetActive(true);
                
                var eventTrigger = _buttons[i].GetComponent<EventTrigger>() 
                                  ?? _buttons[i].gameObject.AddComponent<EventTrigger>();
                
                eventTrigger.triggers.Clear();
                
                var enterEntry = new EventTrigger.Entry();
                enterEntry.eventID = EventTriggerType.PointerEnter;
                enterEntry.callback.AddListener((_) => ShowMoveDescription(moveData));
                eventTrigger.triggers.Add(enterEntry);
                
                var exitEntry = new EventTrigger.Entry();
                exitEntry.eventID = EventTriggerType.PointerExit;
                exitEntry.callback.AddListener((_) => HideMoveDescription());
                eventTrigger.triggers.Add(exitEntry);
            }
    
            SetContainerActive(true);
        }

        private void SetupButton(GameObject buttonObj, string text, Action onClick)
        {
            var button = buttonObj.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClick());
            }
            
            var textComponent = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (textComponent != null)
            {
                textComponent.text = text;
            }
        }

        private void HideAllButtons()
        {
            foreach (var button in _buttons)
            {
                button.SetActive(false);
            }
        }
        
        private void ShowMoveDescription(MoveData moveData)
        {
            string description = $"{moveData.Name}: Power {moveData.Effects[0].Power}, Type: {moveData.Type}\n{moveData.Description}";
            if (_messageUIManager != null)
                _messageUIManager.ShowMessage(description, float.MaxValue);
        }

        private void HideMoveDescription()
        {
            if (_messageUIManager != null)
                _messageUIManager.ClearAllMessages();
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
            
            if (_uiController != null)
                _uiController.OnMenuStateChanged(newState, _playerIndex);
                
            RefreshButtons();
        }

        private void HandleActionSelect(ActionType actionType, int actionIndex)
        {
            _uiController.OnActionSelect(actionType, actionIndex);
        }
    }
}