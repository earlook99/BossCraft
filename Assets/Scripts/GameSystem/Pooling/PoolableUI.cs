// PoolableUI.cs
using UnityEngine;
using System.Threading;

namespace GameSystem.Pooling
{
    public abstract class PoolableUI : MonoBehaviour
    {
        protected bool _isActive = false;
        protected CancellationTokenSource _cts;
        
        public virtual void OnSpawned()
        {
            _isActive = true;
            _cts = new CancellationTokenSource();
        }
        
        public virtual void OnDespawned()
        {
            _isActive = false;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
        
        public virtual void ResetUI()
        {
            _isActive = false;
        }
        
        public void ReturnToPool()
        {
            UIPoolManager.Instance?.ReturnUI(this);
        }
        
        protected virtual void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}