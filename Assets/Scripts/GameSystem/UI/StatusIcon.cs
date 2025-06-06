using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GameSystem.UI
{
    public class StatusIcon : MonoBehaviour
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private TextMeshProUGUI _stackText;
        
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
            
            UpdateStack(stackCount);
            
            // 즉시 표시
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
            }
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
    }
}