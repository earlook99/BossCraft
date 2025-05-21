using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using GameSystem;
using UnityEngine.UIElements;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;

namespace UI
{
    public class ActionButtonUI : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI buttonText;

        private int _actionIndex;

        public event Action<int> OnButtonClicked;

        private void Awake()
        {
            // 만약 Inspector 연결 안 되어 있으면 GetComponent로 찾기
            if (button is null)
            {
                button = GetComponent<Button>();
            }
                
            button.onClick.AddListener(HandleClick);
        }

        public void Setup(string text, int actionIndex)
        {
            if (buttonText is not null)
            {
                buttonText.text = text;
            }
            
            _actionIndex = actionIndex;
        }

        private void HandleClick()
        {
            OnButtonClicked?.Invoke(_actionIndex);
        }

        private void OnDestroy()
        {
            // 이벤트 해제
            if (button is not null)
            {
                button.onClick.RemoveListener(HandleClick);
            }
        }
    }
}