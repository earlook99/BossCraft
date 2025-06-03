using System.Collections.Generic;
using Data;
using UnityEngine;
using Entity;
using UI;
using GameSystem.Events;
using GameSystem.Pooling;

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
        
        [Header("Canvas Assignment")]
        [SerializeField] private CanvasType _statusCanvasType = CanvasType.Dynamic;
        
        [Header("Buff Icons")]
        [SerializeField] private Sprite _attackBuffIcon;
        [SerializeField] private Sprite _defenseBuffIcon;
        
        private Dictionary<int, CharacterStatusUI> _entityStatusUIs = new Dictionary<int, CharacterStatusUI>(5);
        private Dictionary<(int entityIndex, BuffsType buffType), PoolableStatusIcon> _buffIcons = new Dictionary<(int, BuffsType), PoolableStatusIcon>();
        private BattleEntity[] _entities;
        private int[] _lastKnownHP = new int[5];
        private bool _isInitialized = false;
        
        private void Awake()
        {
            SubscribeToEvents();
        }
        
        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }
        
        private void SubscribeToEvents()
        {
            BattleEvents.OnDamageDealt += HandleDamageDealt;
            BattleEvents.OnHealingReceived += HandleHealingReceived;
            BattleEvents.OnShieldActivated += HandleShieldActivated;
            BattleEvents.OnShieldBroken += HandleShieldBroken;
            BattleEvents.OnBuffStackChanged += HandleBuffStackChanged;
            
            UIEvents.OnUpdateHPBar += HandleUpdateHPBar;
            UIEvents.OnUpdateShield += HandleUpdateShield;
        }
        
        private void UnsubscribeFromEvents()
        {
            BattleEvents.OnDamageDealt -= HandleDamageDealt;
            BattleEvents.OnHealingReceived -= HandleHealingReceived;
            BattleEvents.OnShieldActivated -= HandleShieldActivated;
            BattleEvents.OnShieldBroken -= HandleShieldBroken;
            BattleEvents.OnBuffStackChanged -= HandleBuffStackChanged;
            
            UIEvents.OnUpdateHPBar -= HandleUpdateHPBar;
            UIEvents.OnUpdateShield -= HandleUpdateShield;
        }
        
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
        
        private void HandleDamageDealt(DamageDealtEventArgs args)
        {
            UpdateEntityHP(args.Target);
        }
        
        private void HandleHealingReceived(HealingReceivedEventArgs args)
        {
            UpdateEntityHP(args.Target);
        }
        
        private void HandleShieldActivated(ShieldActivatedEventArgs args)
        {
            if (_entityStatusUIs.TryGetValue((int)EntityType.Boss, out var statusUI))
            {
                int previousHP = args.Boss.CurrentHP + args.ShieldHP;
                statusUI.AnimateShieldConversion(previousHP, args.Boss.CurrentHP, args.ShieldHP);
            }
        }
        
        private void HandleShieldBroken(ShieldBrokenEventArgs args)
        {
            UpdateEntityHP(args.Boss);
        }
        
        private void HandleUpdateHPBar(UpdateHPBarEventArgs args)
        {
            if (!_isInitialized) return;
            
            if (args.EntityIndex >= 0 && args.EntityIndex < _lastKnownHP.Length)
            {
                if (_lastKnownHP[args.EntityIndex] != args.CurrentHP)
                {
                    _lastKnownHP[args.EntityIndex] = args.CurrentHP;
                    
                    if (_entityStatusUIs.TryGetValue(args.EntityIndex, out var statusUI))
                    {
                        statusUI.UpdateHP(args.CurrentHP);
                    }
                }
            }
        }
        
        private void HandleUpdateShield(UpdateShieldEventArgs args)
        {
            if (_entityStatusUIs.TryGetValue((int)EntityType.Boss, out var statusUI))
            {
                statusUI.UpdateShield(args.Boss.ShieldStacks, args.Boss.MaxShieldStacks, args.Boss.ShieldHP);
            }
        }
        
        private void HandleBuffStackChanged(BuffStackChangedEventArgs args)
        {
            int entityIndex = System.Array.IndexOf(_entities, args.Entity);
            if (entityIndex < 0) return;
        
            var key = (entityIndex, args.BuffType);
        
            if (args.NewStackCount == 0)
            {
                // 버프 제거
                if (_buffIcons.TryGetValue(key, out var icon))
                {
                    UIPoolManager.Instance?.ReturnUI(icon);
                    _buffIcons.Remove(key);
                }
            }
            else
            {
                // 버프 추가/업데이트
                if (_buffIcons.TryGetValue(key, out var icon))
                {
                    icon.UpdateStack(args.NewStackCount);
                }
                else
                {
                    CreateBuffIcon(entityIndex, args.BuffType, args.NewStackCount);
                }
            }
        }
        
        private void CreateBuffIcon(int entityIndex, BuffsType buffType, int stackCount)
        {
            if (!_entityStatusUIs.TryGetValue(entityIndex, out var statusUI)) return;
        
            var poolManager = UIPoolManager.Instance;
            if (poolManager == null) return;
        
            var icon = poolManager.GetUI<PoolableStatusIcon>(UIPoolManager.STATUS_ICON_POOL);
            if (icon == null) return;
        
            Sprite sprite = buffType == BuffsType.Attack ? _attackBuffIcon : _defenseBuffIcon;
            icon.Setup(sprite, stackCount);
        
            // 아이콘 위치를 해당 엔티티의 StatusUI 근처에 배치
            icon.transform.SetParent(statusUI.transform, false);
        
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