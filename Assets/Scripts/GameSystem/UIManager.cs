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
    public class UIManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BattleManager _battleManager;

        [Header("Battle Entities")]
        [SerializeField] private BattleEntity[] _battleEntities = new BattleEntity[5];

        [Header("UI Containers")]
        [SerializeField] private RectTransform _bossArea;
        [SerializeField] private RectTransform _partyArea;

        [Header("Action Menu")]
        [SerializeField] private GameObject _actionMenuPanel;
        [SerializeField] private ActionMenuUI _actionMenuUI;

        [Header("Target Selection")]
        [SerializeField] private TargetSelectionUI _targetSelectionUI;

        [Header("Other UI")]
        [SerializeField] private GameObject _battleInfo;
        [SerializeField] private BattleEndUI _battleEndUI;

        [Header("Prefabs")]
        [SerializeField] private CharacterStatusUI _playerStatusPrefab;
        [SerializeField] private CharacterStatusUI _bossStatusPrefab;

        private Dictionary<int, CharacterStatusUI> _entityStatusUIs = new Dictionary<int, CharacterStatusUI>();
        private TextMeshProUGUI _battleMessageTextComponent;

        private int _currentPlayerIndex;
        private ActionType _pendingActionType;
        private int _pendingActionIndex;
        
        private void Awake()
        {
            if (_battleManager == null)
                _battleManager = FindAnyObjectByType<BattleManager>();

            if (_targetSelectionUI == null)
            {
                _targetSelectionUI = GetComponentInChildren<TargetSelectionUI>(true);
            
                if (_targetSelectionUI == null)
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
            }
            
            if (_actionMenuPanel != null)
                _actionMenuPanel.SetActive(false);

            if (_battleInfo != null) 
                _battleMessageTextComponent = _battleInfo.GetComponentInChildren<TextMeshProUGUI>();
        }
        
        private void Start()
        {
            _currentPlayerIndex = 0;
            SetupBattleUI();
        }
        
        public void SetupBattleUI()
        {
            CleanupExistingUI();
            CreateBossStatusUI();
            CreatePartyStatusUIs();
            
            if (_targetSelectionUI != null && _battleEntities != null)
                _targetSelectionUI.Setup(this, _battleEntities);
        }
        
        private void CleanupExistingUI()
        {
            foreach (var kv in _entityStatusUIs)
            {
                if (kv.Value != null && kv.Value.gameObject != null)
                    Destroy(kv.Value.gameObject);
            }
            _entityStatusUIs.Clear();
        }
        
        private void CreateBossStatusUI()
        {
            var bossEntity = _battleEntities[(int)EntityType.Boss];
            if (bossEntity == null || _bossArea == null || _bossStatusPrefab == null)
                return;

            var statusUI = Instantiate(_bossStatusPrefab, _bossArea);
            statusUI.Setup(bossEntity, (int)EntityType.Boss);
            _entityStatusUIs.Add((int)EntityType.Boss, statusUI);
        }
        
        private void CreatePartyStatusUIs()
        {
            if (_partyArea == null || _playerStatusPrefab == null)
                return;

            for (int i = 0; i < 4; i++)
            {
                var entity = _battleEntities[i];
                if (entity == null) continue;

                var statusUI = Instantiate(_playerStatusPrefab, _partyArea);
                statusUI.Setup(entity, i);
                _entityStatusUIs.Add(i, statusUI);
            }
        }
        
        public void ShowActionMenuForCurrentPlayer(int currentPlayerIndex)
        {
            _currentPlayerIndex = currentPlayerIndex;
    
            if (_battleEntities == null || _currentPlayerIndex >= _battleEntities.Length)
                return;
    
            var playerEntity = _battleEntities[_currentPlayerIndex];
            if (playerEntity == null)
                return;
    
            if (_actionMenuPanel)
                _actionMenuPanel.SetActive(true);
    
            if (_actionMenuUI)
                _actionMenuUI.Setup(this, playerEntity, _currentPlayerIndex);
        }
        
        public void OnActionSelect(ActionType actionType, int actionIndex)
        {
            if (_actionMenuPanel != null)
                _actionMenuPanel.SetActive(false);
            
            _pendingActionType = actionType;
            _pendingActionIndex = actionIndex;
            
            if (actionType == ActionType.Move)
            {
                var playerEntity = _battleEntities[_currentPlayerIndex];
                var moveData = playerEntity.GetMoveData(actionIndex);
                
                if (moveData != null)
                {
                    if (moveData.Category == MoveCategory.AOE)
                        CompleteActionWithTarget(EntityType.Boss);
                    else
                        StartTargetSelection(moveData);
                }
            }
            else
            {
                CompleteActionWithTarget(EntityType.Boss);
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
            
            if (_battleManager != null)
                _battleManager.ReceivePlayerChoice(newAction);
        }
        
        public IEnumerator ShowBattleMessage(string message, float duration = 2f)
        {
            if (_battleMessageTextComponent != null)
            {
                _battleMessageTextComponent.text = message;
                if (_battleInfo != null) _battleInfo.SetActive(true);
                yield return new WaitForSeconds(duration);
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
        
        public void UpdateShieldUI(BossEntity boss)
        {
            if (_entityStatusUIs.TryGetValue((int)EntityType.Boss, out var statusUI))
                statusUI.UpdateShield(boss.ShieldStacks, boss.MaxShieldStacks, boss.ShieldHP);
        }
        
        public void AnimateShieldConversion(BossEntity boss, int previousHP)
        {
            if (_entityStatusUIs.TryGetValue((int)EntityType.Boss, out var statusUI))
                statusUI.AnimateShieldConversion(previousHP, boss.CurrentHP, boss.ShieldHP);
        }
        
        public void ShowBattleEndScreen(bool isVictory)
        {
            if (_battleEndUI != null)
                _battleEndUI.ShowBattleEnd(isVictory);
        }
    }
}