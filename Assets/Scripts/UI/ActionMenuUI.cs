using System;
using System.Collections.Generic;
using Data;
using UnityEngine;
using GameSystem;
using Entity;
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
        [SerializeField] private Transform buttonsContainer;
        [SerializeField] private GameObject buttonPrefab;
        [SerializeField] private int maxButtonCount = 8;

        private BattleUIController uiController;
        private BattleEntity currentEntity;
        private int playerIndex;
        private MenuState currentState;
        
        private List<GameObject> buttons = new List<GameObject>(8);
        private CanvasGroup containerCanvasGroup;
        private bool isInitialized = false;

        private const float ACTION_MESSAGE_DURATION = 1f;
        
        private readonly Action[] mainMenuActions = new Action[4];
        private readonly string[] mainMenuLabels = { "Fight", "Item", "Guard", "Taunt" };
        
        private Button[] buttonComponents;
        private TextMeshProUGUI[] buttonTexts;
        private EventTrigger[] eventTriggers;

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (buttonsContainer != null)
            {
                containerCanvasGroup = buttonsContainer.GetComponent<CanvasGroup>();
                if (containerCanvasGroup == null)
                {
                    containerCanvasGroup = buttonsContainer.gameObject.AddComponent<CanvasGroup>();
                }
            }
            
            CreateButtons();
            CacheButtonComponents();
            
            mainMenuActions[0] = () => SwitchMenuState(MenuState.Moves);
            mainMenuActions[1] = () => SwitchMenuState(MenuState.Items);
            mainMenuActions[2] = () => HandleActionSelect(ActionType.Guard, -1);
            mainMenuActions[3] = () => HandleActionSelect(ActionType.Taunt, -1);
            
            isInitialized = true;
        }

        private void CreateButtons()
        {
            for (int i = 0; i < maxButtonCount; i++)
            {
                GameObject buttonObj = Instantiate(buttonPrefab, buttonsContainer);
                buttonObj.SetActive(false);
                buttons.Add(buttonObj);
            }
        }
        
        private void CacheButtonComponents()
        {
            buttonComponents = new Button[maxButtonCount];
            buttonTexts = new TextMeshProUGUI[maxButtonCount];
            eventTriggers = new EventTrigger[maxButtonCount];
            
            for (int i = 0; i < buttons.Count; i++)
            {
                buttonComponents[i] = buttons[i].GetComponent<Button>();
                buttonTexts[i] = buttons[i].GetComponentInChildren<TextMeshProUGUI>();
                
                eventTriggers[i] = buttons[i].GetComponent<EventTrigger>();
                if (eventTriggers[i] == null)
                    eventTriggers[i] = buttons[i].AddComponent<EventTrigger>();
            }
        }

        public void Setup(BattleUIController controller, BattleEntity entity, int playerIndex)
        {
            if (!isInitialized)
                Initialize();
            
            uiController = controller;
            currentEntity = entity;
            this.playerIndex = playerIndex;
            currentState = MenuState.Main;
            
            if (entity == null) return;

            RefreshButtons();
        }

        private void RefreshButtons()
        {
            if (uiController != null)
                uiController.ShowMessage($"What will {currentEntity.EntityName} do?", ACTION_MESSAGE_DURATION);
            
            switch (currentState)
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
            
            for (int i = 0; i < mainMenuLabels.Length && i < buttons.Count; i++)
            {
                SetupButton(i, mainMenuLabels[i], mainMenuActions[i]);
                buttons[i].SetActive(true);
            }
            
            SetContainerActive(true);
        }

        private void ShowMoveMenu()
        {
            HideAllButtons();
            
            ReadOnlySpan<MoveInstance> currentMoves = currentEntity.MoveInstances;
            int moveCount = currentMoves.Length;

            for (int i = 0; i < moveCount && i < buttons.Count; i++)
            {
                int index = i;
                var moveData = currentMoves[i].Data;
                
                SetupButton(i, moveData.Name, () => HandleActionSelect(ActionType.Move, index));
                buttons[i].SetActive(true);
                
                eventTriggers[i].triggers.Clear();
                
                var enterEntry = new EventTrigger.Entry();
                enterEntry.eventID = EventTriggerType.PointerEnter;
                enterEntry.callback.AddListener((_) => ShowMoveDescription(moveData));
                eventTriggers[i].triggers.Add(enterEntry);
                
                var exitEntry = new EventTrigger.Entry();
                exitEntry.eventID = EventTriggerType.PointerExit;
                exitEntry.callback.AddListener((_) => HideMoveDescription());
                eventTriggers[i].triggers.Add(exitEntry);
            }
    
            SetContainerActive(true);
        }

        private void SetupButton(int index, string text, Action onClick)
        {
            if (buttonComponents[index] != null)
            {
                buttonComponents[index].onClick.RemoveAllListeners();
                buttonComponents[index].onClick.AddListener(() => onClick());
            }
            
            if (buttonTexts[index] != null)
            {
                buttonTexts[index].text = text;
            }
        }

        private void HideAllButtons()
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                buttons[i].SetActive(false);
            }
        }
        
        private void ShowMoveDescription(MoveData moveData)
        {
            string description = $"{moveData.Name}: Power {moveData.Effects[0].Power}, Type: {moveData.Type}\n{moveData.Description}";
            if (uiController != null)
                uiController.ShowMessage(description, float.MaxValue);
        }

        private void HideMoveDescription()
        {
            if (uiController != null)
                uiController.ShowMessage("", 0f);
        }

        private void SetContainerActive(bool active)
        {
            if (containerCanvasGroup != null)
            {
                containerCanvasGroup.alpha = active ? 1f : 0f;
                containerCanvasGroup.interactable = active;
                containerCanvasGroup.blocksRaycasts = active;
            }
        }

        private void SwitchMenuState(MenuState newState)
        {
            currentState = newState;
            
            if (uiController != null)
                uiController.OnMenuStateChanged(newState, playerIndex);
                
            RefreshButtons();
        }

        private void HandleActionSelect(ActionType actionType, int actionIndex)
        {
            uiController.OnActionSelect(actionType, actionIndex);
        }
    }
}