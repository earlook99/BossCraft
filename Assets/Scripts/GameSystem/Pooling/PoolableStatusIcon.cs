// PoolableStatusIcon.cs
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GameSystem.Utils;

namespace GameSystem.Pooling
{
    public class PoolableStatusIcon : PoolableUI
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private TextMeshProUGUI _stackText;
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
        
        public void Setup(Sprite icon, int stackCount = 1, int duration = -1)
        {
            if (_iconImage != null)
            {
                _iconImage.sprite = icon;
            }
            
            if (_stackText != null)
            {
                if (stackCount > 1)
                {
                    _stackText.text = stackCount.ToString();
                    _stackText.gameObject.SetActive(true);
                }
                else
                {
                    _stackText.gameObject.SetActive(false);
                }
            }
            
            _ = FadeInAsync(_cts.Token);
        }
        
        public void UpdateStack(int stackCount)
        {
            if (_stackText != null)
            {
                if (stackCount > 1)
                {
                    _stackText.text = stackCount.ToString();
                    _stackText.gameObject.SetActive(true);
                }
                else
                {
                    _stackText.gameObject.SetActive(false);
                }
            }
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
            
            if (_stackText != null)
            {
                _stackText.text = string.Empty;
                _stackText.gameObject.SetActive(false);
            }
        }
    }
}