using UnityEngine;
using System.Collections;

namespace Effect
{
    public class PoolableEffect : MonoBehaviour
    {
        [Header("Auto Return Settings")]
        [SerializeField] private bool _autoReturn = true;
        [SerializeField] private float _lifetime = 2f;
        
        private Coroutine _autoReturnCoroutine;
        private ParticleSystem[] _particleSystems;
        private Animator _animator;
        
        private void Awake()
        {
            _particleSystems = GetComponentsInChildren<ParticleSystem>();
            _animator = GetComponent<Animator>();
        }
        
        public void OnSpawned()
        {
            if (_particleSystems != null)
            {
                foreach (var ps in _particleSystems)
                {
                    ps.Play();
                }
            }
            
            if (_animator != null)
            {
                _animator.SetTrigger("Play");
            }
            
            if (_autoReturn)
            {
                _autoReturnCoroutine = StartCoroutine(AutoReturnAfterDelay());
            }
        }
        
        public void OnDespawned()
        {
            if (_autoReturnCoroutine != null)
            {
                StopCoroutine(_autoReturnCoroutine);
                _autoReturnCoroutine = null;
            }
            
            if (_particleSystems != null)
            {
                foreach (var ps in _particleSystems)
                {
                    ps.Stop();
                    ps.Clear();
                }
            }
            
            if (_animator != null)
            {
                _animator.ResetTrigger("Play");
            }
        }
        
        private IEnumerator AutoReturnAfterDelay()
        {
            yield return new WaitForSeconds(_lifetime);
            ReturnToPool();
        }
        
        public void ReturnToPool()
        {
            var poolManager = GameSystem.Pooling.EffectPoolManager.Instance;
            if (poolManager != null)
            {
                poolManager.ReturnEffect(this);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
        
        public void OnAnimationEnd()
        {
            ReturnToPool();
        }
        
        public void SetLifetime(float lifetime)
        {
            _lifetime = lifetime;
        }
        
        public float GetMaxParticleDuration()
        {
            float maxDuration = 0f;
            
            if (_particleSystems != null)
            {
                foreach (var ps in _particleSystems)
                {
                    if (ps.main.loop)
                    {
                        return _lifetime;
                    }
                    
                    float duration = ps.main.duration + ps.main.startLifetime.constantMax;
                    if (duration > maxDuration)
                    {
                        maxDuration = duration;
                    }
                }
            }
            
            return maxDuration;
        }
    }
}