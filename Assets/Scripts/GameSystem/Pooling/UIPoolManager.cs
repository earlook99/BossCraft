using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GameSystem.Pooling
{
    public class UIPoolManager : MonoBehaviour
    {
        private static UIPoolManager _instance;
        public static UIPoolManager Instance => _instance;
        
        [System.Serializable]
        public class UIPoolConfig
        {
            public string PoolName;
            public GameObject Prefab;
            public Transform Parent;
            public int InitialSize = 10;
            public int MaxSize = 50;
        }
        
        [Header("Pool Configuration")]
        [SerializeField] private List<UIPoolConfig> _poolConfigs;
        [SerializeField] private Canvas _uiCanvas;
        
        private Dictionary<string, GenericObjectPool<PoolableUI>> _uiPools;
        
        public const string DAMAGE_TEXT_POOL = "DamageText";
        public const string BUTTON_POOL = "ActionButton";
        public const string STATUS_ICON_POOL = "StatusIcon";
        
        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                InitializePools();
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        private void OnDestroy()
        {
            if (_instance == this)
            {
                ClearAllPools();
                _instance = null;
            }
        }
        
        private void InitializePools()
        {
            _uiPools = new Dictionary<string, GenericObjectPool<PoolableUI>>();
            
            if (_uiCanvas == null)
            {
                _uiCanvas = GetComponentInParent<Canvas>();
                if (_uiCanvas == null)
                {
                    _uiCanvas = FindObjectOfType<Canvas>();
                }
            }
            
            foreach (var config in _poolConfigs)
            {
                CreatePool(config);
            }
        }
        
        private void CreatePool(UIPoolConfig config)
        {
            if (string.IsNullOrEmpty(config.PoolName) || config.Prefab == null)
            {
                Debug.LogWarning("Invalid UI pool configuration");
                return;
            }
            
            if (_uiPools.ContainsKey(config.PoolName))
            {
                Debug.LogWarning($"UI Pool with name {config.PoolName} already exists");
                return;
            }
            
            EnsurePoolableComponent(config.Prefab);
            
            Transform parent = config.Parent ?? _uiCanvas.transform;
            
            var pool = new GenericObjectPool<PoolableUI>(
                config.Prefab,
                parent,
                config.InitialSize,
                config.MaxSize,
                ResetUI
            );
            
            _uiPools.Add(config.PoolName, pool);
        }
        
        private void EnsurePoolableComponent(GameObject prefab)
        {
            if (prefab.GetComponent<PoolableUI>() == null)
            {
                prefab.AddComponent<PoolableUI>();
            }
        }
        
        private bool ResetUI(PoolableUI ui)
        {
            if (ui == null) return false;
            
            ui.ResetUI();
            
            var rectTransform = ui.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = Vector2.zero;
                rectTransform.localScale = Vector3.one;
            }
            
            return true;
        }
        
        public PoolableUI GetUI(string poolName)
        {
            if (!_uiPools.TryGetValue(poolName, out var pool))
            {
                Debug.LogWarning($"UI Pool {poolName} not found");
                return null;
            }
            
            var ui = pool.Get();
            if (ui != null)
            {
                ui.OnSpawned();
            }
            
            return ui;
        }
        
        public T GetUI<T>(string poolName) where T : PoolableUI
        {
            return GetUI(poolName) as T;
        }
        
        public void ReturnUI(PoolableUI ui)
        {
            if (ui == null) return;
            
            ui.OnDespawned();
            
            foreach (var kvp in _uiPools)
            {
                if (ui.name.Contains(kvp.Key))
                {
                    kvp.Value.Return(ui);
                    return;
                }
            }
            
            Destroy(ui.gameObject);
        }
        
        public PoolableDamageText ShowDamageText(Vector3 worldPosition, int damage, Color color)
        {
            var damageText = GetUI<PoolableDamageText>(DAMAGE_TEXT_POOL);
            if (damageText != null)
            {
                damageText.Setup(worldPosition, damage, color);
            }
            return damageText;
        }
        
        public PoolableButton CreateButton(string text, System.Action onClick, Transform parent = null)
        {
            var button = GetUI<PoolableButton>(BUTTON_POOL);
            if (button != null)
            {
                if (parent != null)
                {
                    button.transform.SetParent(parent, false);
                }
                button.Setup(text, onClick);
            }
            return button;
        }
        
        private void ClearAllPools()
        {
            foreach (var pool in _uiPools.Values)
            {
                pool.Clear();
            }
            _uiPools.Clear();
        }
    }
}