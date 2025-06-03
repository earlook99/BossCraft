using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GameSystem.Utils;

namespace GameSystem.Pooling
{
    public abstract class PoolableUI : MonoBehaviour
    {
        protected bool _isActive = false;
        protected CancellationTokenSource _cts;
        
        public virtual void OnSpawned()
        {
            _isActive = true;
            _cts = new CancellationTokenSource();
        }
        
        public virtual void OnDespawned()
        {
            _isActive = false;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
        
        public virtual void ResetUI()
        {
            _isActive = false;
        }
        
        public void ReturnToPool()
        {
            UIPoolManager.Instance?.ReturnUI(this);
        }
        
        protected virtual void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
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
            
            _ = AnimateDamageTextAsync(_cts.Token);
        }
        
        private async Task AnimateDamageTextAsync(CancellationToken ct)
        {
            float elapsed = 0f;
            Vector2 startPosition = _rectTransform.anchoredPosition;
            
            while (elapsed < _lifetime && !ct.IsCancellationRequested)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / _lifetime;
                
                _rectTransform.anchoredPosition = startPosition + Vector2.up * (_moveSpeed * elapsed);
                
                if (progress > 0.5f)
                {
                    float fadeProgress = (progress - 0.5f) * 2f;
                    _canvasGroup.alpha = 1f - fadeProgress;
                }
                
                await AsyncUtilities.NextFrameAsync(ct);
            }
            
            if (!ct.IsCancellationRequested)
            {
                ReturnToPool();
            }
        }
        
        public override void OnDespawned()
        {
            base.OnDespawned();
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
        
        protected override void OnDestroy()
        {
            base.OnDestroy();
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
            
            _ = FadeInAsync(_cts.Token);
        }
        
        private async Task FadeInAsync(CancellationToken ct)
        {
            float elapsed = 0f;
            _canvasGroup.alpha = 0f;
            
            while (elapsed < _fadeInDuration && !ct.IsCancellationRequested)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = elapsed / _fadeInDuration;
                await AsyncUtilities.NextFrameAsync(ct);
            }
            
            if (!ct.IsCancellationRequested)
            {
                _canvasGroup.alpha = 1f;
            }
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