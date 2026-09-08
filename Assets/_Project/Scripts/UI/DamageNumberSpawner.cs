using System.Collections.Generic;
using UnityEngine;
using ChibiRift.Core;

namespace ChibiRift.UI
{
    /// <summary>
    /// Spawns a floating number for every resolved hit (HPS-008).
    /// </summary>
    /// <remarks>
    /// Everything arrives through <see cref="DamageAppliedEvent"/> on the <see cref="EventBus"/>.
    /// This component holds no reference into ChibiRift.Gameplay and could not acquire one: the
    /// assembly graph forbids ChibiRift.UI from referencing it at all (SRS 26). The event carries
    /// a world position, which is projected to screen space here.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class DamageNumberSpawner : MonoBehaviour
    {
        [Header("Pooling (SRS 29)")]
        [Tooltip("Prefab pooled for each number. Must carry a DamageNumber and sit under a Screen Space Overlay canvas.")]
        [SerializeField] private DamageNumber _prefab;

        [Tooltip("Parent for pooled instances. Defaults to this transform.")]
        [SerializeField] private RectTransform _container;

        [Tooltip("Instances created up front so the first hits of a wave do not allocate.")]
        [Min(0)]
        [SerializeField] private int _prewarmCount = 16;

        [Tooltip("Ceiling on live numbers. Beyond this the oldest is recycled rather than a new one created.")]
        [Min(1)]
        [SerializeField] private int _maxCount = 64;

        private EventBus _eventBus;
        private ObjectPool<DamageNumber> _pool;
        private readonly List<DamageNumber> _live = new List<DamageNumber>();
        private Camera _camera;

        private void Awake()
        {
            if (_container == null) _container = transform as RectTransform;

            if (_prefab == null)
            {
                GameLog.Error("UI", $"{name} has no DamageNumber prefab; hits will show no number (HPS-008).");
                return;
            }

            _pool = new ObjectPool<DamageNumber>(_prefab, _container, _prewarmCount, _maxCount);
        }

        private void OnEnable()
        {
            if (ServiceLocator.Current == null) return;
            if (!ServiceLocator.Current.TryGet(out _eventBus)) return;

            _eventBus.Subscribe<DamageAppliedEvent>(OnDamageApplied);
        }

        private void OnDisable()
        {
            // Unsubscribing matters: the EventBus outlives the scene, so a handler left behind
            // would keep a destroyed component alive and fire into it on the next Run.
            _eventBus?.Unsubscribe<DamageAppliedEvent>(OnDamageApplied);
            _eventBus = null;
        }

        private void Update()
        {
            // Recycled from here rather than from the number itself: the pool is owned here, and a
            // pooled object that returns itself is easy to double-release.
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                DamageNumber number = _live[i];
                if (number == null)
                {
                    _live.RemoveAt(i);
                    continue;
                }

                if (!number.IsFinished) continue;

                _live.RemoveAt(i);
                _pool.Release(number);
            }
        }

        private void OnDamageApplied(DamageAppliedEvent evt)
        {
            if (_pool == null) return;

            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            Vector2 screenPosition = _camera.WorldToScreenPoint(evt.WorldPosition);

            DamageNumber number = _pool.Get();
            if (number == null) return;

            number.Play(evt.Result.FinalDamage, evt.Result.WasCritical, screenPosition);
            _live.Add(number);
        }
    }
}
