using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using GameSystem.Events;

namespace GameSystem.UI
{
    public class MessageUIManager : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject _battleMessagePanel;
        [SerializeField] private TextMeshProUGUI _battleMessageText;
        
        private Queue<MessageData> _messageQueue = new Queue<MessageData>();
        private Coroutine _messageCoroutine;
        private bool _isShowingMessage = false;
        
        private const float DEFAULT_MESSAGE_DURATION = 2f;
        
        private struct MessageData
        {
            public string Message;
            public float Duration;
            
            public MessageData(string message, float duration)
            {
                Message = message;
                Duration = duration;
            }
        }
        
        private void Awake()
        {
            InitializeReferences();
            SubscribeToEvents();
        }
        
        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            
            if (_messageCoroutine != null)
            {
                StopCoroutine(_messageCoroutine);
            }
        }
        
        private void InitializeReferences()
        {
            if (_battleMessagePanel == null)
            {
                _battleMessagePanel = GameObject.Find("BattleInfo");
            }
            
            if (_battleMessageText == null && _battleMessagePanel != null)
            {
                _battleMessageText = _battleMessagePanel.GetComponentInChildren<TextMeshProUGUI>();
            }
        }
        
        private void SubscribeToEvents()
        {
            UIEvents.OnShowMessage += HandleShowMessage;
        }
        
        private void UnsubscribeFromEvents()
        {
            UIEvents.OnShowMessage -= HandleShowMessage;
        }
        
        private void HandleShowMessage(ShowMessageEventArgs args)
        {
            ShowMessage(args.Message, args.Duration);
        }
        
        public void ShowMessage(string message, float duration = DEFAULT_MESSAGE_DURATION)
        {
            _messageQueue.Enqueue(new MessageData(message, duration));
            
            if (!_isShowingMessage)
            {
                _messageCoroutine = StartCoroutine(ProcessMessageQueue());
            }
        }
        
        public void ShowMessageImmediate(string message, float duration = DEFAULT_MESSAGE_DURATION)
        {
            if (_messageCoroutine != null)
            {
                StopCoroutine(_messageCoroutine);
            }
            
            _messageQueue.Clear();
            _messageQueue.Enqueue(new MessageData(message, duration));
            _messageCoroutine = StartCoroutine(ProcessMessageQueue());
        }
        
        private IEnumerator ProcessMessageQueue()
        {
            _isShowingMessage = true;
            
            while (_messageQueue.Count > 0)
            {
                var messageData = _messageQueue.Dequeue();
                yield return DisplayMessage(messageData.Message, messageData.Duration);
            }
            
            _isShowingMessage = false;
        }
        
        private IEnumerator DisplayMessage(string message, float duration)
        {
            if (_battleMessageText != null)
            {
                _battleMessageText.text = message;
                
                if (_battleMessagePanel != null && !_battleMessagePanel.activeSelf)
                {
                    _battleMessagePanel.SetActive(true);
                }
                
                yield return new WaitForSeconds(duration);
                
                if (_messageQueue.Count == 0 && _battleMessagePanel != null)
                {
                    _battleMessagePanel.SetActive(false);
                }
            }
        }
        
        public void ClearAllMessages()
        {
            _messageQueue.Clear();
            
            if (_messageCoroutine != null)
            {
                StopCoroutine(_messageCoroutine);
                _messageCoroutine = null;
            }
            
            _isShowingMessage = false;
            
            if (_battleMessagePanel != null)
            {
                _battleMessagePanel.SetActive(false);
            }
        }
        
        public bool HasPendingMessages()
        {
            return _messageQueue.Count > 0 || _isShowingMessage;
        }
    }
}