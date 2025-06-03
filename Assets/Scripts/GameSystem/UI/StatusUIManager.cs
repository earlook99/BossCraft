using System.Collections.Generic;
using UnityEngine;
using Entity;
using UI;
using GameSystem.Events;

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
        
        private Dictionary<int, CharacterStatusUI> _entityStatusUIs = new Dictionary<int, CharacterStatusUI>();
        private BattleEntity[] _entities;
        
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
            
            UIEvents.OnUpdateHPBar += HandleUpdateHPBar;
            UIEvents.OnUpdateShield += HandleUpdateShield;
        }
        
        private void UnsubscribeFromEvents()
        {
            BattleEvents.OnDamageDealt -= HandleDamageDealt;
            BattleEvents.OnHealingReceived -= HandleHealingReceived;
            BattleEvents.OnShieldActivated -= HandleShieldActivated;
            BattleEvents.OnShieldBroken -= HandleShieldBroken;
            
            UIEvents.OnUpdateHPBar -= HandleUpdateHPBar;
            UIEvents.OnUpdateShield -= HandleUpdateShield;
        }
        
        public void Initialize(BattleEntity[] entities)
        {
            _entities = entities;
            SetupBattleUI();
        }
        
        private void SetupBattleUI()
        {
            CleanupExistingUI();
            CreateBossStatusUI();
            CreatePartyStatusUIs();
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
            if (_entityStatusUIs.TryGetValue(args.EntityIndex, out var statusUI))
            {
                statusUI.UpdateHP(args.CurrentHP);
            }
        }
        
        private void HandleUpdateShield(UpdateShieldEventArgs args)
        {
            if (_entityStatusUIs.TryGetValue((int)EntityType.Boss, out var statusUI))
            {
                statusUI.UpdateShield(args.Boss.ShieldStacks, args.Boss.MaxShieldStacks, args.Boss.ShieldHP);
            }
        }
        
        private void UpdateEntityHP(BattleEntity entity)
        {
            int entityIndex = System.Array.IndexOf(_entities, entity);
            if (entityIndex >= 0 && _entityStatusUIs.TryGetValue(entityIndex, out var statusUI))
            {
                statusUI.UpdateHP(entity.CurrentHP);
            }
        }
    }
}