using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GameSystem.Pooling
{
    public class PoolableDamageText : PoolableUI
    {
        [SerializeField] private TextMeshProUGUI _damageText;
        [SerializeField] private float _moveSpeed = 100f;
        [SerializeField] private float _fadeSpeed = 1f;
        [SerializeField] private float _lifetime = 1.5f;
        
        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private Coroutine _animationCoroutine;
        
        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
            
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
        
        public void Setup(Vector3 worldPosition, int damage, Color color)
        {
            if (_damageText != null)
            {
                _damageText.text = damage.ToString();
                _damageText.color = color;
            }
            
            Vector2 screenPosition = Camera.main.WorldToScreenPoint(worldPosition);
            _rectTransform.position = screenPosition;
            
            _canvasGroup.alpha = 1f;
            
            if (_animationCoroutine != null)
                StopCoroutine(_animationCoroutine);
                
            _animationCoroutine = StartCoroutine(AnimateDamageText());
        }
        
        private IEnumerator AnimateDamageText()
        {
            float elapsed = 0f;
            Vector2 startPosition = _rectTransform.anchoredPosition;
            
            while (elapsed < _lifetime)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / _lifetime;
                
                _rectTransform.anchoredPosition = startPosition + Vector2.up * (_moveSpeed * elapsed);
                
                if (progress > 0.5f)
                {
                    float fadeProgress = (progress - 0.5f) * 2f;
                    _canvasGroup.alpha = 1f - fadeProgress;
                }
                
                yield return null;
            }
            
            ReturnToPool();
        }
        
        public override void OnDespawned()
        {
            base.OnDespawned();
            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
                _animationCoroutine = null;
            }
        }
        
        public override void ResetUI()
        {
            base.ResetUI();
            _canvasGroup.alpha = 1f;
        }
    }
}