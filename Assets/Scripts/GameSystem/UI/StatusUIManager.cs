using System.Collections.Generic;
using Data;
using UnityEngine;
using Entity;
using UI;

namespace GameSystem.UI
{
    public class StatusUIManager : MonoBehaviour
    {
        [Header("UI Containers")]
        [SerializeField] private RectTransform _bossArea;
        [SerializeField] private RectTransform _partyArea;
        
        [Header("Prefabs")]
        [SerializeField] private CharacterStatusUI _playerStatusPrefab;
        [SerializeField] private CharacterStatusUI _bossStatusPrefab;
        [SerializeField] private GameObject _statusIconPrefab;
        [SerializeField] private GameObject _damageTextPrefab;
        
        [Header("Canvas Assignment")]
        [SerializeField] private CanvasType _statusCanvasType = CanvasType.Dynamic;
        
        [Header("Buff Icons")]
        [SerializeField] private Sprite _attackBuffIcon;
        [SerializeField] private Sprite _defenseBuffIcon;
        
        private Dictionary<int, CharacterStatusUI> _entityStatusUIs = new Dictionary<int, CharacterStatusUI>(5);
        private Dictionary<(int entityIndex, BuffsType buffType), StatusIcon> _buffIcons = new Dictionary<(int, BuffsType), StatusIcon>();
        private BattleEntity[] _entities;
        private int[] _lastKnownHP = new int[5];
        private bool _isInitialized = false;
        
        public void Initialize(BattleEntity[] entities)
        {
            _entities = entities;
            SetupBattleUI();
            _isInitialized = true;
        }
        
        private void SetupBattleUI()
        {
            CleanupExistingUI();
            
            var canvasOptimizer = CanvasOptimizer.Instance;
            
            CreateBossStatusUI();
            CreatePartyStatusUIs();
            
            if (canvasOptimizer != null)
            {
                if (_bossArea != null)
                {
                    canvasOptimizer.MoveToCanvas(_bossArea.gameObject, _statusCanvasType);
                    canvasOptimizer.OptimizeUIElement(_bossArea.gameObject);
                }
                
                if (_partyArea != null)
                {
                    canvasOptimizer.MoveToCanvas(_partyArea.gameObject, _statusCanvasType);
                    canvasOptimizer.OptimizeUIElement(_partyArea.gameObject);
                }
            }
            
            InitializeHPTracking();
        }
        
        private void InitializeHPTracking()
        {
            for (int i = 0; i < _entities.Length && i < _lastKnownHP.Length; i++)
            {
                if (_entities[i] != null)
                {
                    _lastKnownHP[i] = _entities[i].CurrentHP;
                }
            }
        }
        
        private void CleanupExistingUI()
        {
            foreach (var kvp in _entityStatusUIs)
            {
                if (kvp.Value != null && kvp.Value.gameObject != null)
                    Destroy(kvp.Value.gameObject);
            }
            _entityStatusUIs.Clear();
            
            foreach (var kvp in _buffIcons)
            {
                if (kvp.Value != null && kvp.Value.gameObject != null)
                    Destroy(kvp.Value.gameObject);
            }
            _buffIcons.Clear();
        }
        
        private void CreateBossStatusUI()
        {
            if (_entities == null || _entities.Length < 5) return;
            
            var bossEntity = _entities[(int)EntityType.Boss];
            if (bossEntity == null || _bossArea == null || _bossStatusPrefab == null)
                return;
                
            var statusUI = Instantiate(_bossStatusPrefab, _bossArea);
            statusUI.Setup(bossEntity, (int)EntityType.Boss);
            _entityStatusUIs.Add((int)EntityType.Boss, statusUI);
            
            OptimizeStatusUI(statusUI.gameObject);
        }
        
        private void CreatePartyStatusUIs()
        {
            if (_entities == null || _partyArea == null || _playerStatusPrefab == null)
                return;
                
            for (int i = 0; i < 4; i++)
            {
                if (i >= _entities.Length) break;
                
                var entity = _entities[i];
                if (entity == null) continue;
                
                var statusUI = Instantiate(_playerStatusPrefab, _partyArea);
                statusUI.Setup(entity, i);
                _entityStatusUIs.Add(i, statusUI);
                
                OptimizeStatusUI(statusUI.gameObject);
            }
        }
        
        private void OptimizeStatusUI(GameObject statusUIObject)
        {
            var canvasOptimizer = CanvasOptimizer.Instance;
            if (canvasOptimizer != null)
            {
                canvasOptimizer.OptimizeUIElement(statusUIObject);
            }
        }
        
        public void OnDamageDealt(BattleEntity target)
        {
            UpdateEntityHP(target);
            ShowDamageText(target);
        }
        
        private void ShowDamageText(BattleEntity target)
        {
            if (_damageTextPrefab == null) return;
            
            var canvas = CanvasOptimizer.Instance?.GetCanvas(CanvasType.Dynamic);
            if (canvas == null) return;
            
            GameObject damageTextObj = Instantiate(_damageTextPrefab, canvas.transform);
            DamageText damageText = damageTextObj.GetComponent<DamageText>();
            
            if (damageText != null)
            {
                int damage = _lastKnownHP[System.Array.IndexOf(_entities, target)] - target.CurrentHP;
                damageText.Setup(target.transform.position, Mathf.Abs(damage), Color.red);
            }
        }
        
        public void OnHealingReceived(BattleEntity target)
        {
            UpdateEntityHP(target);
        }
        
        public void OnShieldActivated(BossEntity boss, int shieldHP)
        {
            if (_entityStatusUIs.TryGetValue((int)EntityType.Boss, out var statusUI))
            {
                int previousHP = boss.CurrentHP + shieldHP;
                statusUI.AnimateShieldConversion(previousHP, boss.CurrentHP, shieldHP);
            }
        }
        
        public void OnShieldBroken(BossEntity boss)
        {
            UpdateEntityHP(boss);
        }
        
        public void UpdateHPBar(int entityIndex, int currentHP, int maxHP)
        {
            if (!_isInitialized) return;
            
            if (entityIndex >= 0 && entityIndex < _lastKnownHP.Length)
            {
                if (_lastKnownHP[entityIndex] != currentHP)
                {
                    _lastKnownHP[entityIndex] = currentHP;
                    
                    if (_entityStatusUIs.TryGetValue(entityIndex, out var statusUI))
                    {
                        statusUI.UpdateHP(currentHP);
                    }
                }
            }
        }
        
        public void UpdateShield(BossEntity boss)
        {
            if (_entityStatusUIs.TryGetValue((int)EntityType.Boss, out var statusUI))
            {
                statusUI.UpdateShield(boss.ShieldStacks, boss.MaxShieldStacks, boss.ShieldHP);
            }
        }
        
        public void OnBuffStackChanged(BattleEntity entity, BuffsType buffType, int newStackCount)
        {
            int entityIndex = System.Array.IndexOf(_entities, entity);
            if (entityIndex < 0) return;
        
            var key = (entityIndex, buffType);
        
            if (newStackCount == 0)
            {
                if (_buffIcons.TryGetValue(key, out var icon))
                {
                    Destroy(icon.gameObject);
                    _buffIcons.Remove(key);
                }
            }
            else
            {
                if (_buffIcons.TryGetValue(key, out var icon))
                {
                    icon.UpdateStack(newStackCount);
                }
                else
                {
                    CreateBuffIcon(entityIndex, buffType, newStackCount);
                }
            }
        }
        
        private void CreateBuffIcon(int entityIndex, BuffsType buffType, int stackCount)
        {
            if (!_entityStatusUIs.TryGetValue(entityIndex, out var statusUI)) return;
            if (_statusIconPrefab == null) return;
        
            GameObject iconObj = Instantiate(_statusIconPrefab, statusUI.transform);
            StatusIcon icon = iconObj.GetComponent<StatusIcon>();
            
            if (icon == null)
            {
                icon = iconObj.AddComponent<StatusIcon>();
            }
        
            Sprite sprite = buffType == BuffsType.Attack ? _attackBuffIcon : _defenseBuffIcon;
            icon.Setup(sprite, stackCount);
        
            _buffIcons[(entityIndex, buffType)] = icon;
        }
        
        private void UpdateEntityHP(BattleEntity entity)
        {
            if (!_isInitialized || entity == null) return;
            
            int entityIndex = System.Array.IndexOf(_entities, entity);
            if (entityIndex >= 0 && entityIndex < _lastKnownHP.Length)
            {
                int currentHP = entity.CurrentHP;
                
                if (_lastKnownHP[entityIndex] != currentHP)
                {
                    _lastKnownHP[entityIndex] = currentHP;
                    
                    if (_entityStatusUIs.TryGetValue(entityIndex, out var statusUI))
                    {
                        statusUI.UpdateHP(currentHP);
                    }
                }
            }
        }
    }
}