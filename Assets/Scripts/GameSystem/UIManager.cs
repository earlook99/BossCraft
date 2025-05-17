using System;
using System.Collections.Generic;
using Entity;
using UI;
using UnityEngine;

namespace GameSystem
{
    public enum ActionType
    {
        Move,
        Item
        // etc...
    }

    public enum EntityType
    {
        Character1,
        Character2,
        Character3,
        Character4,
        Boss
    }
    
    public struct ActionData
    {
        public ActionType Action;
        public int ActionIndex;
        public EntityType Source;
        public EntityType Target;

        public ActionData(ActionType move, int moveIndex, EntityType boss, EntityType character1)
        {
            throw new NotImplementedException();
        }
    }
    public class UIManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BattleManager _battleManager;
        
        [Header("UI Containers")]
        [SerializeField] private Transform _bossArea;
        [SerializeField] private Transform _partyArea;
        [SerializeField] private GameObject _actionMenu;
        [SerializeField] private GameObject _battleInfo;
        
        [Header("UI Prefabs")]
        [SerializeField] private GameObject _characterStatusPrefab;
        [SerializeField] private GameObject _actionButtonPrefab;
        
        private List<ActionData> _playerChoices = new List<ActionData>();
        private int _currentPlayerIndex;
        private BattleEntity[] _battleEntities;
        
        public event Action<List<ActionData>> OnAllChoicesComplete;
        
        private void Start()
        {
            if (!_battleManager)
            {
                _battleManager = FindFirstObjectByType<BattleManager>();
            }
            
            SetupBattleUI();
        }

        private void SetupBattleUI()
        {
            _battleEntities = _battleManager.Entities;
            _playerChoices = new List<ActionData>();
            _currentPlayerIndex = 0;
            
            CreateEntityStatusUI();
            
            _actionMenu.SetActive(false);
        }
        
        private void CreateEntityStatusUI()
        {
            var bossEntity = _battleEntities[(int)EntityType.Boss];
            if (bossEntity != null)
            {
                GameObject bossStatusObj = Instantiate(_characterStatusPrefab, _bossArea);
                CharacterStatusUI bossStatusUI = bossStatusObj.GetComponent<CharacterStatusUI>();
                bossStatusUI.Setup(bossEntity, (int)EntityType.Boss);
        
                RectTransform bossRect = bossStatusObj.GetComponent<RectTransform>();
                bossRect.sizeDelta = new Vector2(300, 150);
            }
    
            for (int i = 0; i < 4; i++)
            {
                var playerEntity = _battleEntities[i];
                if (playerEntity != null)
                {
                    GameObject playerStatusObj = Instantiate(_characterStatusPrefab, _partyArea);
                    CharacterStatusUI playerStatusUI = playerStatusObj.GetComponent<CharacterStatusUI>();
                    playerStatusUI.Setup(playerEntity, i);
                }
            }
        }
        
        public void StartPlayerTurn()
        {
            _currentPlayerIndex = 0;
            _playerChoices.Clear();
            ShowActionMenuForCurrentPlayer();
        }
        
        private void ShowActionMenuForCurrentPlayer()
        {
            _actionMenu.SetActive(true);
            
            BattleEntity currentPlayerEntity = _battleEntities[_currentPlayerIndex];
    
            ActionMenuUI actionMenuUI = _actionMenu.GetComponent<ActionMenuUI>();
            if (actionMenuUI != null)
            {
                actionMenuUI.Setup(currentPlayerEntity, _currentPlayerIndex, _battleManager.TurnCount);
            }
        }
        
        public void OnActionSelected(ActionType actionType, int actionIndex)
        {
            ActionData newAction = new ActionData
            {
                Action = actionType,
                ActionIndex = actionIndex,
                Source = (EntityType)_currentPlayerIndex,
                Target = EntityType.Boss
            };
    
            _playerChoices.Add(newAction);
    
            _currentPlayerIndex++;
    
            if (_currentPlayerIndex >= 4)
            {
                _actionMenu.SetActive(false);
                OnAllChoicesComplete?.Invoke(_playerChoices);
            }
            else
            {
                ShowActionMenuForCurrentPlayer();
            }
        }
        
        public void ShowBattleMessage(string message, float duration = 2f)
        {
            // 전투 메시지 표시...
        }
        
        public void UpdateEntityUI(int entityIndex)
        {
            if (entityIndex < 0 || entityIndex >= _battleEntities.Length)
            {
                return;
            }

            CharacterStatusUI[] allStatusUIs = FindObjectsByType<CharacterStatusUI>(FindObjectsSortMode.None);            
            foreach (var statusUI in allStatusUIs)
            {
                if (statusUI.EntityIndex == entityIndex)
                {
                    statusUI.UpdateHP();
                    break;
                }
            }
        }
    }
}
