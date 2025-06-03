using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Entity;

namespace GameSystem
{
    public class ActionQueue : MonoBehaviour
    {
        private Queue<QueuedAction> _actionQueue = new Queue<QueuedAction>();
        private Coroutine _processCoroutine;
        private bool _isProcessing = false;
        private bool _isPaused = false;
        
        public int QueuedCount => _actionQueue.Count;
        public bool IsProcessing => _isProcessing;
        public bool IsPaused => _isPaused;
        
        public event Action<QueuedAction> OnActionStarted;
        public event Action<QueuedAction> OnActionCompleted;
        public event Action OnQueueEmpty;
        
        public class QueuedAction
        {
            public string ActionId { get; }
            public ActionData Data { get; }
            public float Priority { get; }
            public Func<IEnumerator> Executor { get; }
            public Action<bool> Callback { get; }
            public float QueueTime { get; }
            
            public QueuedAction(ActionData data, Func<IEnumerator> executor, float priority = 0f, Action<bool> callback = null)
            {
                ActionId = Guid.NewGuid().ToString();
                Data = data;
                Executor = executor;
                Priority = priority;
                Callback = callback;
                QueueTime = Time.time;
            }
        }
        
        public void EnqueueAction(ActionData action, Func<IEnumerator> executor, float priority = 0f, Action<bool> callback = null)
        {
            var queuedAction = new QueuedAction(action, executor, priority, callback);
            
            if (priority > 0)
            {
                InsertByPriority(queuedAction);
            }
            else
            {
                _actionQueue.Enqueue(queuedAction);
            }
            
            if (!_isProcessing && !_isPaused)
            {
                StartProcessing();
            }
        }
        
        private void InsertByPriority(QueuedAction newAction)
        {
            var tempList = new List<QueuedAction>(_actionQueue);
            _actionQueue.Clear();
            
            bool inserted = false;
            foreach (var action in tempList)
            {
                if (!inserted && newAction.Priority > action.Priority)
                {
                    _actionQueue.Enqueue(newAction);
                    inserted = true;
                }
                _actionQueue.Enqueue(action);
            }
            
            if (!inserted)
            {
                _actionQueue.Enqueue(newAction);
            }
        }
        
        public void EnqueueImmediate(ActionData action, Func<IEnumerator> executor, Action<bool> callback = null)
        {
            StopProcessing();
            
            var immediateAction = new QueuedAction(action, executor, float.MaxValue, callback);
            
            var tempQueue = new Queue<QueuedAction>();
            tempQueue.Enqueue(immediateAction);
            
            while (_actionQueue.Count > 0)
            {
                tempQueue.Enqueue(_actionQueue.Dequeue());
            }
            
            _actionQueue = tempQueue;
            StartProcessing();
        }
        
        private void StartProcessing()
        {
            if (_processCoroutine != null)
            {
                StopCoroutine(_processCoroutine);
            }
            
            _processCoroutine = StartCoroutine(ProcessQueue());
        }
        
        private void StopProcessing()
        {
            if (_processCoroutine != null)
            {
                StopCoroutine(_processCoroutine);
                _processCoroutine = null;
            }
            _isProcessing = false;
        }
        
        private IEnumerator ProcessQueue()
        {
            _isProcessing = true;
            
            while (_actionQueue.Count > 0 && !_isPaused)
            {
                var action = _actionQueue.Dequeue();
                
                OnActionStarted?.Invoke(action);
                
                bool success = false;
                IEnumerator executorCoroutine = null;
                
                try
                {
                    if (action.Executor != null)
                    {
                        executorCoroutine = action.Executor();
                        success = true;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error executing action: {e.Message}");
                    success = false;
                }
                
                if (executorCoroutine != null && success)
                {
                    yield return executorCoroutine;
                }
                
                action.Callback?.Invoke(success);
                OnActionCompleted?.Invoke(action);
                
                yield return null;
            }
            
            _isProcessing = false;
            
            if (_actionQueue.Count == 0)
            {
                OnQueueEmpty?.Invoke();
            }
        }
        
        public void Pause()
        {
            _isPaused = true;
        }
        
        public void Resume()
        {
            _isPaused = false;
            
            if (!_isProcessing && _actionQueue.Count > 0)
            {
                StartProcessing();
            }
        }
        
        public void Clear()
        {
            StopProcessing();
            _actionQueue.Clear();
        }
        
        public void ClearExcept(Predicate<ActionData> keepCondition)
        {
            var tempList = new List<QueuedAction>();
            
            while (_actionQueue.Count > 0)
            {
                var action = _actionQueue.Dequeue();
                if (keepCondition(action.Data))
                {
                    tempList.Add(action);
                }
            }
            
            foreach (var action in tempList)
            {
                _actionQueue.Enqueue(action);
            }
        }
        
        public bool HasAction(Predicate<ActionData> condition)
        {
            foreach (var action in _actionQueue)
            {
                if (condition(action.Data))
                {
                    return true;
                }
            }
            return false;
        }
        
        public int CountActions(Predicate<ActionData> condition)
        {
            int count = 0;
            foreach (var action in _actionQueue)
            {
                if (condition(action.Data))
                {
                    count++;
                }
            }
            return count;
        }
        
        private void OnDestroy()
        {
            StopProcessing();
        }
    }
}