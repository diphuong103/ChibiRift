using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChibiRift.Core
{
    /// <summary>
    /// Reusable pool for projectiles, VFX and enemies (SRS 29: "ưu tiên object pooling cho
    /// projectile/VFX/enemy"). Avoids the Instantiate/Destroy churn that would break the frame
    /// time budget of NFR-001 and NFR-002 during dense waves.
    /// </summary>
    /// <typeparam name="T">Component type carried by the pooled prefab.</typeparam>
    public sealed class ObjectPool<T> : IDisposable where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Stack<T> _available;
        private readonly HashSet<T> _active = new HashSet<T>();
        private readonly int _maxSize;

        /// <summary>Instances currently checked out.</summary>
        public int ActiveCount => _active.Count;

        /// <summary>Instances sitting in the pool.</summary>
        public int AvailableCount => _available.Count;

        /// <summary>Instances created since construction. Used to verify pooling in perf tests.</summary>
        public int TotalCreated { get; private set; }

        /// <param name="prefab">Prefab to clone. Must carry a <typeparamref name="T"/>.</param>
        /// <param name="parent">Container for inactive instances, keeping the hierarchy tidy.</param>
        /// <param name="prewarmCount">Instances to create up front so the first wave does not hitch (NFR-002).</param>
        /// <param name="maxSize">Cap on retained instances. Extra returns are destroyed instead of retained.</param>
        public ObjectPool(T prefab, Transform parent = null, int prewarmCount = 0, int maxSize = 256)
        {
            _prefab = prefab != null ? prefab : throw new ArgumentNullException(nameof(prefab));
            _parent = parent;
            _maxSize = Mathf.Max(1, maxSize);
            _available = new Stack<T>(Mathf.Max(prewarmCount, 8));

            for (int i = 0; i < prewarmCount; i++)
            {
                T instance = CreateInstance();
                instance.gameObject.SetActive(false);
                _available.Push(instance);
            }
        }

        /// <summary>Checks out an instance, creating one only when the pool is empty.</summary>
        public T Get()
        {
            T instance = _available.Count > 0 ? _available.Pop() : CreateInstance();

            _active.Add(instance);
            instance.gameObject.SetActive(true);

            if (instance is IPoolable poolable) poolable.OnSpawnedFromPool();
            return instance;
        }

        /// <summary>Checks out an instance and places it in the world.</summary>
        public T Get(Vector3 position, Quaternion rotation)
        {
            T instance = Get();
            instance.transform.SetPositionAndRotation(position, rotation);
            return instance;
        }

        /// <summary>
        /// Returns an instance. Double-returns are ignored rather than corrupting the pool,
        /// which matters because enemy death can be raised from several code paths (HPS-004).
        /// </summary>
        public void Release(T instance)
        {
            if (instance == null) return;
            if (!_active.Remove(instance)) return;

            if (instance is IPoolable poolable) poolable.OnReturnedToPool();

            instance.gameObject.SetActive(false);

            if (_available.Count >= _maxSize)
            {
                UnityEngine.Object.Destroy(instance.gameObject);
                return;
            }

            if (_parent != null) instance.transform.SetParent(_parent, false);
            _available.Push(instance);
        }

        /// <summary>Returns every checked-out instance. Used when a wave or run tears down.</summary>
        public void ReleaseAll()
        {
            if (_active.Count == 0) return;

            var snapshot = new List<T>(_active);
            for (int i = 0; i < snapshot.Count; i++) Release(snapshot[i]);
        }

        /// <summary>Destroys every instance owned by this pool.</summary>
        public void Dispose()
        {
            ReleaseAll();
            while (_available.Count > 0)
            {
                T instance = _available.Pop();
                if (instance != null) UnityEngine.Object.Destroy(instance.gameObject);
            }
        }

        private T CreateInstance()
        {
            T instance = UnityEngine.Object.Instantiate(_prefab, _parent);
            instance.name = $"{_prefab.name}_Pooled_{TotalCreated:D3}";
            TotalCreated++;
            return instance;
        }
    }
}
