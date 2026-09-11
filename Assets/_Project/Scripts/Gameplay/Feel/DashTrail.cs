using System.Collections.Generic;
using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Afterimages left behind during a dash (MOV-006).
    /// </summary>
    /// <remarks>
    /// A dash lasts 0.25s and covers 5 units at nearly three times running speed. Without a trail
    /// it reads as a teleport: the hero is simply somewhere else. The afterimages are what make the
    /// path visible, which is what lets a player learn the dash's reach and time it against an
    /// enemy's 0.35s telegraph.
    ///
    /// <para>Pooled through <see cref="ObjectPool{T}"/> (SRS 29). One dash spawns six or seven
    /// ghosts and a player may dash every 1.5s, so per-ghost instantiation would be steady garbage
    /// for no reason.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class DashTrail : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("Supplies the ghost interval, lifetime and alpha (SRS 35).")]
        [SerializeField] private BalanceConfig _balanceConfig;

        [Header("Pooling (SRS 29)")]
        [Tooltip("Afterimage prefab: a SpriteRenderer and nothing else.")]
        [SerializeField] private SpriteRenderer _ghostPrefab;

        [Tooltip("Scene-level parent for the afterimages. Must NOT be the hero: a ghost parented to the hero is dragged along by them and marks nothing.")]
        [SerializeField] private Transform _container;

        [Tooltip("Sprite copied into each afterimage. Defaults to this object's own.")]
        [SerializeField] private SpriteRenderer _source;

        [Tooltip("Afterimages created up front. A dash needs about six.")]
        [Min(0)]
        [SerializeField] private int _prewarmCount = 8;

        private readonly List<Ghost> _live = new List<Ghost>();
        private PlayerMotor _motor;
        private ObjectPool<SpriteRenderer> _pool;
        private float _nextGhostTimer;

        private ImpactConfig Config => _balanceConfig != null ? _balanceConfig.Impact : default;

        private struct Ghost
        {
            public SpriteRenderer Renderer;
            public float Remaining;
            public float Lifetime;
        }

        private void Awake()
        {
            _motor = GetComponentInParent<PlayerMotor>();
            if (_source == null) _source = GetComponentInParent<SpriteRenderer>();

            if (_ghostPrefab == null)
            {
                GameLog.Error("Feel", $"{name} has no ghost prefab; the dash leaves no trail (MOV-006).");
                return;
            }

            // Not this transform: this component is a child of the hero, so ghosts parented here
            // would travel with the hero instead of staying where they were left — which is the
            // one thing an afterimage exists to do.
            if (_container == null) _container = new GameObject("DashGhosts").transform;

            _pool = new ObjectPool<SpriteRenderer>(_ghostPrefab, _container, _prewarmCount);
        }

        /// <summary>Label this instance's Update reports under for NFR-002 profiling.</summary>
        private const string AllocationLabel = "DashTrail.Update";

        private void Update()
        {
            // NFR-002 profiling (OI-32). try/finally: not dashing is the common case and returns
            // early.
            AllocationProfiler.BeginSample(AllocationLabel);
            try
            {
                FadeLiveGhosts();

                if (_pool == null || _motor == null || _source == null) return;

                if (!_motor.IsDashing)
                {
                    // Reset so the first frame of the next dash leaves a ghost straight away.
                    _nextGhostTimer = 0f;
                    return;
                }

                _nextGhostTimer -= Time.deltaTime;
                if (_nextGhostTimer > 0f) return;

                _nextGhostTimer = Config.DashGhostInterval;
                SpawnGhost();
            }
            finally
            {
                AllocationProfiler.EndSample(AllocationLabel);
            }
        }

        private void SpawnGhost()
        {
            SpriteRenderer ghost = _pool.Get(_source.transform.position, _source.transform.rotation);
            if (ghost == null) return;

            ghost.sprite = _source.sprite;
            ghost.drawMode = _source.drawMode;
            ghost.size = _source.size;
            ghost.flipX = _source.flipX;
            ghost.sortingLayerID = _source.sortingLayerID;

            // Behind the hero, so the trail never hides the thing it is describing (SRS 21).
            ghost.sortingOrder = _source.sortingOrder - 1;

            Color tint = _source.color;
            tint.a = Config.DashGhostAlpha;
            ghost.color = tint;

            _live.Add(new Ghost
            {
                Renderer = ghost,
                Remaining = Config.DashGhostLifetime,
                Lifetime = Config.DashGhostLifetime
            });
        }

        private void FadeLiveGhosts()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Ghost ghost = _live[i];
                ghost.Remaining -= Time.deltaTime;

                if (ghost.Remaining <= 0f || ghost.Renderer == null)
                {
                    if (ghost.Renderer != null) _pool.Release(ghost.Renderer);
                    _live.RemoveAt(i);
                    continue;
                }

                float fraction = ghost.Lifetime > 0f ? ghost.Remaining / ghost.Lifetime : 0f;
                Color tint = ghost.Renderer.color;
                tint.a = Config.DashGhostAlpha * fraction;
                ghost.Renderer.color = tint;

                _live[i] = ghost;
            }
        }
    }
}
