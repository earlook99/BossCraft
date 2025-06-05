using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameSystem
{
    public class ActionQueue : MonoBehaviour
    {
        private Queue<QueuedAction> _actionQueue = new Queue<QueuedAction>();
        private Coroutine _processCoroutine;
        private bool _isProcessing = false;
        
        public int QueuedCount => _actionQueue.Count;
        public bool IsProcessing => _isProcessing;
        
        public event Action<QueuedAction> OnActionStarted;
        public event Action<QueuedAction> OnActionCompleted;
        public event Action OnQueueEmpty;
        
        public class QueuedAction
        {
            public string ActionId { get; }
            public ActionData Data { get; }
            public Func<IEnumerator> Executor { get; }
            public Action<bool> Callback { get; }
            
            public QueuedAction(ActionData data, Func<IEnumerator> executor, Action<bool> callback = null)
            {
                ActionId = Guid.NewGuid().ToString();
                Data = data;
                Executor = executor;
                Callback = callback;
            }
        }
        
        public void EnqueueAction(ActionData action, Func<IEnumerator> executor, Action<bool> callback = null)
        {
            var queuedAction = new QueuedAction(action, executor, callback);
            _actionQueue.Enqueue(queuedAction);
            
            if (!_isProcessing)
            {
                StartProcessing();
            }
        }
        
        private void StartProcessing()
        {
            if (_processCoroutine != null)
            {
                StopCoroutine(_processCoroutine);
            }
            
            _processCoroutine = StartCoroutine(ProcessQueue());
        }
        
        private IEnumerator ProcessQueue()
        {
            _isProcessing = true;
            
            while (_actionQueue.Count > 0)
            {
                var action = _actionQueue.Dequeue();
                
                OnActionStarted?.Invoke(action);
                
                bool success = false;
                
                // Executor가 null이 아니면 실행
                if (action.Executor != null)
                {
                    yield return StartCoroutine(action.Executor());
                    success = true;  // Coroutine이 완료되면 성공으로 간주
                }
                
                action.Callback?.Invoke(success);
                OnActionCompleted?.Invoke(action);
                
                yield return null;
            }
            
            _isProcessing = false;
            OnQueueEmpty?.Invoke();
        }
        
        public void Clear()
        {
            if (_processCoroutine != null)
            {
                StopCoroutine(_processCoroutine);
                _processCoroutine = null;
            }
            
            _actionQueue.Clear();
            _isProcessing = false;
        }
        
        private void OnDestroy()
        {
            Clear();
        }
    }
}