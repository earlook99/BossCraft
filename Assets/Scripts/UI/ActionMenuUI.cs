using System;
using System.Collections.Generic;
using Data;
using UnityEngine;
using GameSystem;
using Entity;
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
        [SerializeField] private Transform ButtonsContainer; 
        [SerializeField] private GameObject ButtonPrefab;

        private UIManager _uiManager;
        private BattleEntity _currentEntity;
        private int _playerIndex;
        private MenuState _currentState;

        public void Setup(UIManager manager, BattleEntity entity, int playerIndex)
        {
            _uiManager = manager;
            _currentEntity = entity;
            _playerIndex = playerIndex;
            _currentState = MenuState.Main;
            
            if (entity == null)
            {
                return;
            }

            ClearButtons();
            CreateActionButtons();
        }

        private void ClearButtons()
        {
            foreach (Transform child in ButtonsContainer)
            {
                Destroy(child.gameObject);
            }
        }

        private void CreateActionButtons()
        {
            CreateButton("Fight", () => SwitchMenuState(MenuState.Moves));
            CreateButton("Item",  () => SwitchMenuState(MenuState.Items));
            CreateButton("Guard",  () => HandleActionSelect(ActionType.Guard, -1));
            CreateButton("Taunt",  () => HandleActionSelect(ActionType.Taunt, -1));
        }

        private void CreateMoveButtons()
        {
            ReadOnlySpan<MoveInstance> currentMoves = _currentEntity.MoveInstances;

            for (int i = 0; i < currentMoves.Length; i++)
            {
                int index = i;
                CreateButton(currentMoves[i].Data.Name, () => HandleActionSelect(ActionType.Move, index));
            }
        }

        private void CreateButton(string label, Action onClick)
        {
            var buttonObj = Instantiate(ButtonPrefab, ButtonsContainer);
            var buttonUI = buttonObj.GetComponent<ActionButtonUI>();

            buttonUI.Setup(label, 0);
            buttonUI.OnButtonClicked += (aIndex) =>
            {
                onClick?.Invoke();
            };
        }

        private void SwitchMenuState(MenuState newState)
        {
            _currentState = newState;
            
            ClearButtons();

            switch (newState)
            {
                case MenuState.Main:
                {
                    CreateActionButtons();
                    break;
                }

                case MenuState.Moves:
                {
                    CreateMoveButtons();
                    break;
                }

                case MenuState.Items:
                {
                    // TODO: CreateItemButtons();
                    break;
                }
                    
                default: break;
            }
        }

        private void HandleActionSelect(ActionType actionType, int actionIndex)
        {
            _uiManager.OnActionSelect(actionType, actionIndex);
        }
    }
}
