using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace UI
{
    public class ActionButtonUI : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI buttonText;
        
        private int _actionIndex;
        private GameSystem.ActionType _actionType;
        
        public event Action<GameSystem.ActionType, int> OnButtonClicked;
        
        private void Awake()
        {
            if (button is null)
            {
                button = GetComponent<Button>();
            }

            if (buttonText is null)
            {
                buttonText = GetComponentInChildren<TextMeshProUGUI>();
            }
            
            button.onClick.AddListener(HandleClick);
        }
        
        public void Setup(string text, GameSystem.ActionType actionType, int actionIndex)
        {
            buttonText.text = text;
            _actionType = actionType;
            _actionIndex = actionIndex;
        }
        
        private void HandleClick()
        {
            OnButtonClicked?.Invoke(_actionType, _actionIndex);
        }
        
        private void OnDestroy()
        {
            button.onClick.RemoveListener(HandleClick);
        }
    }
}