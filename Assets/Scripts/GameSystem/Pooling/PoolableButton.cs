// PoolableButton.cs
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GameSystem.Pooling
{
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
}