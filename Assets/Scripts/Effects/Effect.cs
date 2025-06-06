using UnityEngine;
using System.Collections;

namespace Effects
{
    public class Effect : MonoBehaviour
    {
        [Header("Auto Destroy Settings")]
        [SerializeField] private bool _autoDestroy = true;
        [SerializeField] private float _lifetime = 2f;
        
        private Animator _animator;
        
        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }
        
        private void OnEnable()
        {
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                // Play 트리거가 있는지 확인
                bool hasPlayTrigger = false;
                foreach (var param in _animator.parameters)
                {
                    if (param.name == "Play" && param.type == AnimatorControllerParameterType.Trigger)
                    {
                        hasPlayTrigger = true;
                        break;
                    }
                }
                
                if (hasPlayTrigger)
                {
                    _animator.SetTrigger("Play");
                }
            }
            
            if (_autoDestroy)
            {
                StartCoroutine(AutoDestroyAfterDelay());
            }
        }
        
        public float GetDuration()
        {
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                AnimatorClipInfo[] clipInfo = _animator.GetCurrentAnimatorClipInfo(0);
                if (clipInfo.Length > 0)
                {
                    return clipInfo[0].clip.length;
                }
                
                AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.length > 0)
                {
                    return stateInfo.length;
                }
            }
            
            return _lifetime;
        }
        
        private IEnumerator AutoDestroyAfterDelay()
        {
            yield return new WaitForSeconds(GetDuration());
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