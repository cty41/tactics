using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics.Common.Skills.Graph
{
    /// <summary>
    /// Replays transient particle prefabs with deterministic reset and cancellation cleanup.
    /// </summary>
    internal static class TransientVfxPool
    {
        private const int MaxCachedInstancesPerPrefab = 8;
        private static readonly Dictionary<int, Stack<GameObject>> Available = new();
        private static Transform _poolRoot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Available.Clear();
            if (_poolRoot != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(_poolRoot.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(_poolRoot.gameObject);
            }

            _poolRoot = null;
        }

        public static GameObject Rent(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            float scale,
            int sortingLayerId,
            int sortingOrder)
        {
            if (prefab == null)
                return null;

            int key = prefab.GetInstanceID();
            if (!Available.TryGetValue(key, out var stack))
            {
                stack = new Stack<GameObject>();
                Available[key] = stack;
            }

            GameObject instance = null;
            while (stack.Count > 0 && instance == null)
                instance = stack.Pop();
            if (instance == null)
                instance = UnityEngine.Object.Instantiate(prefab);

            var marker = instance.GetComponent<TransientVfxPoolMember>() ??
                instance.AddComponent<TransientVfxPoolMember>();
            marker.InitializeComponentCache();
            marker.RestoreSpriteRendererBaseline();
            marker.PrefabKey = key;
            marker.IsPooled = false;
            instance.transform.SetParent(null, false);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);
            instance.SetActive(true);
            marker.ApplySorting(sortingLayerId, sortingOrder);
            marker.RestartParticles();
            return instance;
        }

        public static void Return(GameObject instance)
        {
            if (instance == null)
                return;

            var marker = instance.GetComponent<TransientVfxPoolMember>();
            if (marker == null)
            {
                UnityEngine.Object.Destroy(instance);
                return;
            }
            if (marker.IsPooled)
                return;

            marker.IsPooled = true;

            marker.StopParticles();
            marker.RestoreSpriteRendererBaseline();
            instance.SetActive(false);
            instance.transform.SetParent(PoolRoot, false);

            if (!Available.TryGetValue(marker.PrefabKey, out var stack))
            {
                stack = new Stack<GameObject>();
                Available[marker.PrefabKey] = stack;
            }
            if (stack.Count >= MaxCachedInstancesPerPrefab)
            {
                UnityEngine.Object.Destroy(instance);
                return;
            }

            stack.Push(instance);
        }

        public static async Task PlayAsync(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            float scale,
            float lifetime,
            int sortingLayerId,
            int sortingOrder,
            CancellationToken cancellationToken)
        {
            var instance = Rent(prefab, position, rotation, scale, sortingLayerId, sortingOrder);
            if (instance == null)
                return;

            instance.name = $"{prefab.name}_Vfx";
            try
            {
                await global::Tactics.GameTimeService.DelayScaledAsync(
                    Mathf.Max(0.05f, lifetime),
                    cancellationToken);
            }
            finally
            {
                Return(instance);
            }
        }

        public static Task PlayOneShot(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            float scale,
            float lifetime,
            int sortingLayerId,
            int sortingOrder,
            CancellationToken cancellationToken)
        {
            return PlayAsync(
                prefab,
                position,
                rotation,
                scale,
                lifetime,
                sortingLayerId,
                sortingOrder,
                cancellationToken);
        }

        public static void ApplySorting(GameObject instance, int sortingLayerId, int sortingOrder)
        {
            if (instance == null)
                return;

            var marker = instance.GetComponent<TransientVfxPoolMember>();
            if (marker != null)
            {
                marker.InitializeComponentCache();
                marker.ApplySorting(sortingLayerId, sortingOrder);
                return;
            }

            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sortingLayerID = sortingLayerId;
                renderer.sortingOrder = sortingOrder;
            }
        }

        private static Transform PoolRoot
        {
            get
            {
                if (_poolRoot != null)
                    return _poolRoot;

                var root = new GameObject("TransientVfxPool");
                root.hideFlags = HideFlags.HideInHierarchy;
                if (Application.isPlaying)
                    UnityEngine.Object.DontDestroyOnLoad(root);
                _poolRoot = root.transform;
                return _poolRoot;
            }
        }
    }

    /// <summary>
    /// Stores the source prefab key on a pooled instance.
    /// </summary>
    internal sealed class TransientVfxPoolMember : MonoBehaviour
    {
        private ParticleSystem[] _particleSystems;
        private Renderer[] _renderers;
        private SpriteRendererBaseline[] _spriteRendererBaselines;
        private SpriteRenderer _runtimeSpriteRenderer;

        public int PrefabKey { get; set; }
        public bool IsPooled { get; set; }

        public void InitializeComponentCache()
        {
            if (_particleSystems != null)
                return;

            _particleSystems = GetComponentsInChildren<ParticleSystem>(true);
            _renderers = GetComponentsInChildren<Renderer>(true);
            var spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            _spriteRendererBaselines = new SpriteRendererBaseline[spriteRenderers.Length];
            for (int index = 0; index < spriteRenderers.Length; index++)
                _spriteRendererBaselines[index] = new SpriteRendererBaseline(spriteRenderers[index]);
        }

        public void RegisterRuntimeSpriteRenderer(SpriteRenderer renderer)
        {
            if (renderer == null || IsBaselineSpriteRenderer(renderer))
                return;
            _runtimeSpriteRenderer = renderer;
        }

        public void ApplySorting(int sortingLayerId, int sortingOrder)
        {
            foreach (Renderer renderer in _renderers)
            {
                if (renderer == null)
                    continue;
                renderer.sortingLayerID = sortingLayerId;
                renderer.sortingOrder = sortingOrder;
            }
        }

        public void RestartParticles()
        {
            foreach (ParticleSystem particleSystem in _particleSystems)
            {
                if (particleSystem == null)
                    continue;
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particleSystem.Simulate(0f, true, true, true);
                particleSystem.Play(true);
            }
        }

        public void StopParticles()
        {
            foreach (ParticleSystem particleSystem in _particleSystems)
            {
                if (particleSystem != null)
                    particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        public void RestoreSpriteRendererBaseline()
        {
            if (_spriteRendererBaselines != null)
            {
                foreach (SpriteRendererBaseline baseline in _spriteRendererBaselines)
                    baseline.Restore();
            }

            if (_runtimeSpriteRenderer == null)
                return;
            _runtimeSpriteRenderer.sprite = null;
            _runtimeSpriteRenderer.sharedMaterial = null;
            _runtimeSpriteRenderer.color = Color.white;
            _runtimeSpriteRenderer.enabled = false;
        }

        private bool IsBaselineSpriteRenderer(SpriteRenderer renderer)
        {
            if (_spriteRendererBaselines == null)
                return false;
            foreach (SpriteRendererBaseline baseline in _spriteRendererBaselines)
            {
                if (baseline.Renderer == renderer)
                    return true;
            }

            return false;
        }

        private readonly struct SpriteRendererBaseline
        {
            private readonly bool _enabled;
            private readonly Sprite _sprite;
            private readonly Material _material;
            private readonly Color _color;
            private readonly int _sortingLayerId;
            private readonly int _sortingOrder;

            public SpriteRendererBaseline(SpriteRenderer renderer)
            {
                Renderer = renderer;
                _enabled = renderer.enabled;
                _sprite = renderer.sprite;
                _material = renderer.sharedMaterial;
                _color = renderer.color;
                _sortingLayerId = renderer.sortingLayerID;
                _sortingOrder = renderer.sortingOrder;
            }

            public SpriteRenderer Renderer { get; }

            public void Restore()
            {
                if (Renderer == null)
                    return;
                Renderer.enabled = _enabled;
                Renderer.sprite = _sprite;
                Renderer.sharedMaterial = _material;
                Renderer.color = _color;
                Renderer.sortingLayerID = _sortingLayerId;
                Renderer.sortingOrder = _sortingOrder;
            }
        }
    }
}
