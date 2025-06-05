using UnityEngine;

namespace GameSystem.Pooling
{
    public abstract class PoolableUI : MonoBehaviour
    {
        protected bool _isActive = false;
        
        public virtual void OnSpawned()
        {
            _isActive = true;
        }
        
        public virtual void OnDespawned()
        {
            _isActive = false;
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
            // 자식 클래스에서 override 가능
        }
    }
}