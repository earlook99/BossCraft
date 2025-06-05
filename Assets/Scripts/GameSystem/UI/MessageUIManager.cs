using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace GameSystem.UI
{
    public class MessageUIManager : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject _battleMessagePanel;
        [SerializeField] private TextMeshProUGUI _battleMessageText;
        [SerializeField] private CanvasGroup _messageCanvasGroup;
        
        [Header("Animation Settings")]
        [SerializeField] private float _fadeInDuration = 0.2f;
        [SerializeField] private float _fadeOutDuration = 0.3f;
        
        private Queue<MessageData> _messageQueue = new Queue<MessageData>();
        private Coroutine _messageCoroutine;
        private bool _isShowingMessage = false;
        private bool _isPanelVisible = false;
        private string _lastMessage = "";
        
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
        }
        
        private void OnDestroy()
        {
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
            
            if (_messageCanvasGroup == null && _battleMessagePanel != null)
            {
                _messageCanvasGroup = _battleMessagePanel.GetComponent<CanvasGroup>();
                if (_messageCanvasGroup == null)
                {
                    _messageCanvasGroup = _battleMessagePanel.AddComponent<CanvasGroup>();
                }
            }
            
            if (_battleMessagePanel != null)
            {
                SetPanelVisibility(true, true);
            }
            
            OptimizeMessagePanel();
        }
        
        private void OptimizeMessagePanel()
        {
            if (_battleMessageText != null)
            {
                _battleMessageText.raycastTarget = false;
            }
            
            var canvasOptimizer = CanvasOptimizer.Instance;
            if (canvasOptimizer != null && _battleMessagePanel != null)
            {
                canvasOptimizer.MoveToCanvas(_battleMessagePanel, CanvasType.Overlay);
                canvasOptimizer.OptimizeUIElement(_battleMessagePanel);
            }
        }
        
        public void ShowMessage(string message, float duration = DEFAULT_MESSAGE_DURATION)
        {
            if (string.IsNullOrEmpty(message)) return;
            
            if (_isShowingMessage && _lastMessage == message)
            {
                return;
            }
            
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
                _lastMessage = message;
                yield break;
            }
        }
        
        private void SetPanelVisibility(bool visible, bool immediate = false)
        {
            if (_messageCanvasGroup != null)
            {
                _messageCanvasGroup.alpha = immediate ? (visible ? 1f : 0f) : _messageCanvasGroup.alpha;
                _messageCanvasGroup.interactable = false;
                _messageCanvasGroup.blocksRaycasts = false;
                
                if (!visible && immediate)
                {
                    _battleMessagePanel.SetActive(false);
                }
                else if (visible)
                {
                    _battleMessagePanel.SetActive(true);
                }
            }
            else if (_battleMessagePanel != null)
            {
                _battleMessagePanel.SetActive(visible);
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
            _lastMessage = "";
            
            SetPanelVisibility(true, true);
            _isPanelVisible = false;
        }
        
        public bool HasPendingMessages()
        {
            return _messageQueue.Count > 0 || _isShowingMessage;
        }
    }
}