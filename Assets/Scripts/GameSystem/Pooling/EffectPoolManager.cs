using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Effect;

namespace GameSystem.Pooling
{
    public class EffectPoolManager : MonoBehaviour
    {
        private static EffectPoolManager _instance;
        public static EffectPoolManager Instance => _instance;
        
        [System.Serializable]
        public class EffectPoolConfig
        {
            public string PoolName;
            public GameObject Prefab;
            public int InitialSize = 5;
            public int MaxSize = 20;
        }
        
        [Header("Pool Configuration")]
        [SerializeField] private List<EffectPoolConfig> _poolConfigs;
        [SerializeField] private Transform _poolParent;
        
        private Dictionary<string, GenericObjectPool<PoolableEffect>> _effectPools;
        private GameObject _defaultEffectPrefab;
        
        private const string DEFAULT_POOL_NAME = "DefaultEffect";
        
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
            _effectPools = new Dictionary<string, GenericObjectPool<PoolableEffect>>();
            
            if (_poolParent == null)
            {
                GameObject poolParentObj = new GameObject("EffectPool");
                poolParentObj.transform.SetParent(transform);
                _poolParent = poolParentObj.transform;
            }
            
            foreach (var config in _poolConfigs)
            {
                CreatePool(config);
            }
        }
        
        private void CreatePool(EffectPoolConfig config)
        {
            if (string.IsNullOrEmpty(config.PoolName) || config.Prefab == null)
            {
                Debug.LogWarning("Invalid pool configuration");
                return;
            }
            
            if (_effectPools.ContainsKey(config.PoolName))
            {
                Debug.LogWarning($"Pool with name {config.PoolName} already exists");
                return;
            }
            
            EnsurePoolableComponent(config.Prefab);
            
            var pool = new GenericObjectPool<PoolableEffect>(
                config.Prefab,
                _poolParent,
                config.InitialSize,
                config.MaxSize,
                ResetEffect
            );
            
            _effectPools.Add(config.PoolName, pool);
            
            if (config.PoolName == DEFAULT_POOL_NAME)
            {
                _defaultEffectPrefab = config.Prefab;
            }
        }
        
        private void EnsurePoolableComponent(GameObject prefab)
        {
            if (prefab.GetComponent<PoolableEffect>() == null)
            {
                prefab.AddComponent<PoolableEffect>();
            }
        }
        
        private bool ResetEffect(PoolableEffect effect)
        {
            if (effect == null) return false;
            
            effect.transform.position = Vector3.zero;
            effect.transform.rotation = Quaternion.identity;
            effect.transform.localScale = Vector3.one;
            
            var particles = effect.GetComponentsInChildren<ParticleSystem>();
            foreach (var particle in particles)
            {
                particle.Clear();
                particle.Stop();
            }
            
            return true;
        }
        
        public PoolableEffect GetEffect(string poolName, Vector3 position, Quaternion rotation)
        {
            if (!_effectPools.TryGetValue(poolName, out var pool))
            {
                Debug.LogWarning($"Pool {poolName} not found, using default");
                
                if (_defaultEffectPrefab != null && !_effectPools.ContainsKey(DEFAULT_POOL_NAME))
                {
                    CreatePool(new EffectPoolConfig
                    {
                        PoolName = DEFAULT_POOL_NAME,
                        Prefab = _defaultEffectPrefab,
                        InitialSize = 10,
                        MaxSize = 30
                    });
                }
                
                if (!_effectPools.TryGetValue(DEFAULT_POOL_NAME, out pool))
                {
                    Debug.LogError("No default effect pool available");
                    return null;
                }
            }
            
            var effect = pool.Get();
            if (effect != null)
            {
                effect.transform.position = position;
                effect.transform.rotation = rotation;
                effect.OnSpawned();
            }
            
            return effect;
        }
        
        public void ReturnEffect(PoolableEffect effect)
        {
            if (effect == null) return;
            
            effect.OnDespawned();
            
            foreach (var kvp in _effectPools)
            {
                if (effect.name.Contains(kvp.Key))
                {
                    kvp.Value.Return(effect);
                    return;
                }
            }
            
            if (_effectPools.TryGetValue(DEFAULT_POOL_NAME, out var defaultPool))
            {
                defaultPool.Return(effect);
            }
            else
            {
                Destroy(effect.gameObject);
            }
        }
        
        public IEnumerator PlayEffectForDuration(string poolName, Vector3 position, float duration)
        {
            var effect = GetEffect(poolName, position, Quaternion.identity);
            if (effect != null)
            {
                yield return new WaitForSeconds(duration);
                ReturnEffect(effect);
            }
        }
        
        public void PreloadEffects(string poolName, int count)
        {
            if (_effectPools.TryGetValue(poolName, out var pool))
            {
                pool.Preload(count);
            }
        }
        
        private void ClearAllPools()
        {
            foreach (var pool in _effectPools.Values)
            {
                pool.Clear();
            }
            _effectPools.Clear();
        }
        
        public Dictionary<string, (int available, int total)> GetPoolStats()
        {
            var stats = new Dictionary<string, (int, int)>();
            
            foreach (var kvp in _effectPools)
            {
                stats[kvp.Key] = (kvp.Value.AvailableCount, kvp.Value.TotalCount);
            }
            
            return stats;
        }
    }
}