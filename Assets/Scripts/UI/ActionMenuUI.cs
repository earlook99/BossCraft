using System.Collections.Generic;
using Data;
using UnityEngine;
using GameSystem;
using Entity;
using TMPro;

namespace UI
{
    public class ActionMenuUI : MonoBehaviour
    {
        [SerializeField] private Transform actionButtonsContainer;
        [SerializeField] private GameObject actionButtonPrefab;
        
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI turnInfoText;
        
        private BattleEntity _currentEntity;
        private List<ActionButtonUI> _spawnedButtons = new List<ActionButtonUI>();
        
        public void Setup(BattleEntity entity, int playerIndex, int turnCount)
        {
            _currentEntity = entity;

            if (playerNameText is not null)
            {
                playerNameText.text = entity.EntityName;
            }

            if (turnInfoText is not null)
            {
                turnInfoText.text = $"턴 {turnCount}";
            }

            ClearButtons();
            CreateActionButtons();
        }
        
        private void ClearButtons()
        {
            foreach (var button in _spawnedButtons)
            {
                Destroy(button.gameObject);
            }
            _spawnedButtons.Clear();
        }
        
        private void CreateActionButtons()
        {
            for (int i = 0; i < _currentEntity.MoveSet.Count; i++)
            {
                MoveData move = _currentEntity.MoveSet[i];
                
                GameObject buttonObj = Instantiate(actionButtonPrefab, actionButtonsContainer);
                ActionButtonUI buttonUI = buttonObj.GetComponent<ActionButtonUI>();
                
                buttonUI.Setup(move.Name, ActionType.Move, i);
                buttonUI.OnButtonClicked += OnActionButtonClicked;
                
                _spawnedButtons.Add(buttonUI);
            }
            
            GameObject itemButtonObj = Instantiate(actionButtonPrefab, actionButtonsContainer);
            ActionButtonUI itemButtonUI = itemButtonObj.GetComponent<ActionButtonUI>();
            
            itemButtonUI.Setup("아이템", ActionType.Item, 0);
            itemButtonUI.OnButtonClicked += OnActionButtonClicked;
            
            _spawnedButtons.Add(itemButtonUI);
        }
        
        private void OnActionButtonClicked(ActionType actionType, int actionIndex)
        {
            UIManager uiManager = FindObjectOfType<UIManager>();
            uiManager.OnActionSelected(actionType, actionIndex);
        }
        
        private void OnDestroy()
        {
            foreach (var button in _spawnedButtons)
            {
                if (button != null)
                    button.OnButtonClicked -= OnActionButtonClicked;
            }
        }
    }
}