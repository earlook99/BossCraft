using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GameSystem.Pooling
{
    public abstract class PoolableUI : MonoBehaviour
    {
        protected bool _isActive = false;
        
        public virtual void OnSpawned()
        {
            _isActive = true;
        }
        
        public virtual void OnDespawned()
        {
            _isActive = false;
        }
        
        public virtual void ResetUI()
        {
            _isActive = false;
        }
        
        public void ReturnToPool()
        {
            UIPoolManager.Instance?.ReturnUI(this);
        }
    }
    
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
            {
                StopCoroutine(_animationCoroutine);
            }
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
    
    public class PoolableButton : PoolableUI
    {
        [SerializeField] private Button _button;
        [SerializeField] private TextMeshProUGUI _buttonText;
        
        private Action _onClick;
        
        private void Awake()
        {
            if (_button == null)
            {
                _button = GetComponent<Button>();
            }
            
            if (_button != null)
            {
                _button.onClick.AddListener(HandleClick);
            }
        }
        
        public void Setup(string text, Action onClick)
        {
            if (_buttonText != null)
            {
                _buttonText.text = text;
            }
            
            _onClick = onClick;
            
            if (_button != null)
            {
                _button.interactable = true;
            }
        }
        
        private void HandleClick()
        {
            _onClick?.Invoke();
        }
        
        public override void OnDespawned()
        {
            base.OnDespawned();
            _onClick = null;
        }
        
        public override void ResetUI()
        {
            base.ResetUI();
            
            _onClick = null;
            
            if (_button != null)
            {
                _button.interactable = true;
            }
            
            if (_buttonText != null)
            {
                _buttonText.text = string.Empty;
            }
        }
        
        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(HandleClick);
            }
        }
    }
    
    public class PoolableStatusIcon : PoolableUI
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private TextMeshProUGUI _durationText;
        [SerializeField] private float _fadeInDuration = 0.3f;
        
        private CanvasGroup _canvasGroup;
        private Coroutine _fadeCoroutine;
        
        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
        
        public void Setup(Sprite icon, int duration = -1)
        {
            if (_iconImage != null)
            {
                _iconImage.sprite = icon;
            }
            
            if (_durationText != null)
            {
                if (duration > 0)
                {
                    _durationText.text = duration.ToString();
                    _durationText.gameObject.SetActive(true);
                }
                else
                {
                    _durationText.gameObject.SetActive(false);
                }
            }
            
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
            }
            _fadeCoroutine = StartCoroutine(FadeIn());
        }
        
        private IEnumerator FadeIn()
        {
            float elapsed = 0f;
            _canvasGroup.alpha = 0f;
            
            while (elapsed < _fadeInDuration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = elapsed / _fadeInDuration;
                yield return null;
            }
            
            _canvasGroup.alpha = 1f;
        }
        
        public void UpdateDuration(int duration)
        {
            if (_durationText != null && duration > 0)
            {
                _durationText.text = duration.ToString();
            }
        }
        
        public override void OnDespawned()
        {
            base.OnDespawned();
            
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }
        }
        
        public override void ResetUI()
        {
            base.ResetUI();
            
            _canvasGroup.alpha = 1f;
            
            if (_iconImage != null)
            {
                _iconImage.sprite = null;
            }
            
            if (_durationText != null)
            {
                _durationText.text = string.Empty;
                _durationText.gameObject.SetActive(false);
            }
        }
    }
}