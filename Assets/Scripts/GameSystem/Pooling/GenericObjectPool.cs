using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameSystem.Pooling
{
    public class GenericObjectPool<T> where T : Component
    {
        private readonly Queue<T> _pool = new Queue<T>();
        private readonly GameObject _prefab;
        private readonly Transform _parent;
        private readonly int _initialSize;
        private readonly int _maxSize;
        private readonly Func<T, bool> _resetAction;
        
        private int _currentSize = 0;
        
        public int AvailableCount => _pool.Count;
        public int TotalCount => _currentSize;
        
        public GenericObjectPool(GameObject prefab, Transform parent, int initialSize = 10, int maxSize = 50, Func<T, bool> resetAction = null)
        {
            _prefab = prefab;
            _parent = parent;
            _initialSize = initialSize;
            _maxSize = maxSize;
            _resetAction = resetAction;
            
            Initialize();
        }
        
        private void Initialize()
        {
            for (int i = 0; i < _initialSize; i++)
            {
                CreateNewObject();
            }
        }
        
        private T CreateNewObject()
        {
            if (_currentSize >= _maxSize)
            {
                Debug.LogWarning($"Object pool reached max size: {_maxSize}");
                return null;
            }
            
            GameObject obj = UnityEngine.Object.Instantiate(_prefab, _parent);
            T component = obj.GetComponent<T>();
            
            if (component == null)
            {
                Debug.LogError($"Prefab does not have component of type {typeof(T)}");
                UnityEngine.Object.Destroy(obj);
                return null;
            }
            
            obj.SetActive(false);
            _currentSize++;
            
            return component;
        }
        
        public T Get()
        {
            T obj = null;
            
            while (_pool.Count > 0 && obj == null)
            {
                obj = _pool.Dequeue();
                if (obj == null || obj.gameObject == null)
                {
                    _currentSize--;
                    obj = null;
                }
            }
            
            if (obj == null)
            {
                obj = CreateNewObject();
                if (obj == null) return null;
            }
            
            obj.gameObject.SetActive(true);
            return obj;
        }
        
        public void Return(T obj)
        {
            if (obj == null || obj.gameObject == null) return;
            
            if (_resetAction != null && !_resetAction(obj))
            {
                Debug.LogWarning("Failed to reset object before returning to pool");
            }
            
            obj.gameObject.SetActive(false);
            
            if (_parent != null)
            {
                obj.transform.SetParent(_parent);
            }
            
            _pool.Enqueue(obj);
        }
        
        public void ReturnAll(List<T> objects)
        {
            foreach (var obj in objects)
            {
                Return(obj);
            }
            objects.Clear();
        }
        
        public void Clear()
        {
            while (_pool.Count > 0)
            {
                var obj = _pool.Dequeue();
                if (obj != null && obj.gameObject != null)
                {
                    UnityEngine.Object.Destroy(obj.gameObject);
                }
            }
            _currentSize = 0;
        }
        
        public void Preload(int count)
        {
            count = Mathf.Min(count, _maxSize - _currentSize);
            
            for (int i = 0; i < count; i++)
            {
                var obj = CreateNewObject();
                if (obj != null)
                {
                    _pool.Enqueue(obj);
                }
            }
        }
    }
}