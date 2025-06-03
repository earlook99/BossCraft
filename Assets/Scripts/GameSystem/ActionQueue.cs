using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Entity;
using GameSystem.Utils;

namespace GameSystem
{
    public class ActionQueue : MonoBehaviour
    {
        private Queue<QueuedAction> _actionQueue = new Queue<QueuedAction>();
        private CancellationTokenSource _processCts;
        private Task _processTask;
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
            public Func<Task> AsyncExecutor { get; }
            public Action<bool> Callback { get; }
            public float QueueTime { get; }
            
            public QueuedAction(ActionData data, Func<Task> asyncExecutor, float priority = 0f, Action<bool> callback = null)
            {
                ActionId = Guid.NewGuid().ToString();
                Data = data;
                AsyncExecutor = asyncExecutor;
                Priority = priority;
                Callback = callback;
                QueueTime = Time.time;
            }
        }
        
        public void EnqueueActionAsync(ActionData action, Func<Task> asyncExecutor, float priority = 0f, Action<bool> callback = null)
        {
            var queuedAction = new QueuedAction(action, asyncExecutor, priority, callback);
            
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
                StartProcessingAsync();
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
        
        public void EnqueueImmediate(ActionData action, Func<Task> asyncExecutor, Action<bool> callback = null)
        {
            StopProcessing();
            
            var immediateAction = new QueuedAction(action, asyncExecutor, float.MaxValue, callback);
            
            var tempQueue = new Queue<QueuedAction>();
            tempQueue.Enqueue(immediateAction);
            
            while (_actionQueue.Count > 0)
            {
                tempQueue.Enqueue(_actionQueue.Dequeue());
            }
            
            _actionQueue = tempQueue;
            StartProcessingAsync();
        }
        
        private async void StartProcessingAsync()
        {
            StopProcessing();
            
            _processCts = new CancellationTokenSource();
            _processTask = ProcessQueueAsync(_processCts.Token);
            
            try
            {
                await _processTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelled
            }
        }
        
        private void StopProcessing()
        {
            _processCts?.Cancel();
            _processCts?.Dispose();
            _processCts = null;
            _processTask = null;
            _isProcessing = false;
        }
        
        private async Task ProcessQueueAsync(CancellationToken ct)
        {
            _isProcessing = true;
            
            while (_actionQueue.Count > 0 && !_isPaused && !ct.IsCancellationRequested)
            {
                var action = _actionQueue.Dequeue();
                
                OnActionStarted?.Invoke(action);
                
                bool success = false;
                
                try
                {
                    if (action.AsyncExecutor != null)
                    {
                        await action.AsyncExecutor();
                        success = true;
                    }
                }
                catch (Exception e) when (!(e is OperationCanceledException))
                {
                    Debug.LogError($"Error executing action: {e.Message}");
                    success = false;
                }
                
                if (!ct.IsCancellationRequested)
                {
                    action.Callback?.Invoke(success);
                    OnActionCompleted?.Invoke(action);
                }
                
                await AsyncUtilities.NextFrameAsync(ct);
            }
            
            _isProcessing = false;
            
            if (_actionQueue.Count == 0 && !ct.IsCancellationRequested)
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
                StartProcessingAsync();
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