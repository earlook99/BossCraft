using System;
using System.Collections.Generic;
using Data;
using UnityEngine;
using GameSystem;
using Entity;
using TMPro;

namespace UI
{
    /// <summary>
    /// Defines the different states of the action menu.
    /// </summary>
    public enum MenuState
    {
        /// <summary>
        /// The main action selection state (Fight, Item, etc.).
        /// </summary>
        Main,
        /// <summary>
        /// The state for selecting a specific move.
        /// </summary>
        Moves,
        /// <summary>
        /// The state for selecting an item.
        /// </summary>
        Items
    }

    /// <summary>
    /// Manages the UI for the player's action menu during battle.
    /// This includes displaying action buttons (Fight, Item, Guard, Taunt) and sub-menus for moves.
    /// </summary>
    public class ActionMenuUI : MonoBehaviour
    {
        [SerializeField] private Transform _buttonsContainer; // Container for the action buttons
        [SerializeField] private GameObject _buttonPrefab; // Prefab for individual action buttons

        private UIManager _uiManager; // Reference to the UIManager
        private BattleEntity _currentEntity; // The entity whose turn it is
        private int _playerIndex; // The index of the current player
        private MenuState _currentState; // The current state of the menu (Main, Moves, Items)

        /// <summary>
        /// Sets up the action menu for the specified entity.
        /// </summary>
        /// <param name="manager">The UIManager instance.</param>
        /// <param name="entity">The BattleEntity whose turn it is.</param>
        /// <param name="playerIndex">The index of the current player.</param>
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

        /// <summary>
        /// Clears all currently displayed action buttons from the container.
        /// </summary>
        private void ClearButtons()
        {
            foreach (Transform child in _buttonsContainer)
            {
                Destroy(child.gameObject);
            }
        }

        /// <summary>
        /// Creates the main set of action buttons (Fight, Item, Guard, Taunt).
        /// </summary>
        private void CreateActionButtons()
        {
            StartCoroutine(_uiManager.ShowBattleMessage($"What will {_currentEntity.EntityName} do?", 1f));
            CreateButton("Fight", () => SwitchMenuState(MenuState.Moves));
            CreateButton("Item",  () => SwitchMenuState(MenuState.Items));
            CreateButton("Guard",  () => HandleActionSelect(ActionType.Guard, -1));
            CreateButton("Taunt",  () => HandleActionSelect(ActionType.Taunt, -1));
        }

        /// <summary>
        /// Creates buttons for each of the current entity's available moves.
        /// </summary>
        private void CreateMoveButtons()
        {
            ReadOnlySpan<MoveInstance> currentMoves = _currentEntity.MoveInstances;

            for (int i = 0; i < currentMoves.Length; i++)
            {
                int index = i;
                CreateButton(currentMoves[i].Data.Name, () => HandleActionSelect(ActionType.Move, index));
            }
        }

        /// <summary>
        /// Instantiates and sets up a single action button.
        /// </summary>
        /// <param name="label">The text label for the button.</param>
        /// <param name="onClick">The action to perform when the button is clicked.</param>
        private void CreateButton(string label, Action onClick)
        {
            var buttonObj = Instantiate(_buttonPrefab, _buttonsContainer);
            var buttonUI = buttonObj.GetComponent<ActionButtonUI>();

            buttonUI.Setup(label, 0); // The '0' here for actionIndex in Setup might be a point of review, but the lambda change is independent
            buttonUI.OnButtonClicked += _ => // Changed (aIndex) to _
            {
                onClick?.Invoke();
            };
        }

        /// <summary>
        /// Switches the menu to a new state, clearing existing buttons and creating new ones for the state.
        /// </summary>
        /// <param name="newState">The <see cref="MenuState"/> to switch to.</param>
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

        /// <summary>
        /// Handles the selection of an action, passing it to the <see cref="UIManager"/>.
        /// </summary>
        /// <param name="actionType">The type of action selected.</param>
        /// <param name="actionIndex">The index of the selected action (e.g., move index).</param>
        private void HandleActionSelect(ActionType actionType, int actionIndex)
        {
            _uiManager.OnActionSelect(actionType, actionIndex);
        }
    }
}
