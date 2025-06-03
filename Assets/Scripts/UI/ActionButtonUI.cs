using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace UI
{
    public class ActionButtonUI : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private TextMeshProUGUI _buttonText;

        private int _actionIndex;

        public event Action<int> OnButtonClicked;

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

        public void Setup(string text, int actionIndex)
        {
            if (_buttonText != null)
            {
                _buttonText.text = text;
            }
            
            _actionIndex = actionIndex;
        }

        private void HandleClick()
        {
            OnButtonClicked?.Invoke(_actionIndex);
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(HandleClick);
            }
        }
    }
}