using UnityEngine;
using System.Collections;

namespace Effect
{
    public class Effect : MonoBehaviour
    {
        [Header("Auto Destroy Settings")]
        [SerializeField] private bool _autoDestroy = true;
        [SerializeField] private float _lifetime = 2f;
        
        private ParticleSystem[] _particleSystems;
        private Animator _animator;
        
        private void Awake()
        {
            _particleSystems = GetComponentsInChildren<ParticleSystem>();
            _animator = GetComponent<Animator>();
        }
        
        private void OnEnable()
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
            
            if (_autoDestroy)
            {
                StartCoroutine(AutoDestroyAfterDelay());
            }
        }
        
        private IEnumerator AutoDestroyAfterDelay()
        {
            yield return new WaitForSeconds(_lifetime);
            Destroy(gameObject);
        }
        
        public void OnAnimationEnd()
        {
            Destroy(gameObject);
        }
        
        public void SetLifetime(float lifetime)
        {
            _lifetime = lifetime;
        }
    }
}