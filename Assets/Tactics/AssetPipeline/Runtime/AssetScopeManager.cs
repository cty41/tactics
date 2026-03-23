using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tactics.AssetPipeline
{
    /// <summary>
    /// Tracks active asset scopes (e.g. scene lifetimes) and defers bundle releases to a safe moment.
    /// </summary>
    public sealed class AssetScopeManager : MonoBehaviourSingleton<AssetScopeManager>
    {
        private readonly List<AssetScope> _scopeStack = new List<AssetScope>();
        private readonly DeferredReleaseQueue _deferredQueue = new DeferredReleaseQueue();

        private Coroutine _flushCoroutine;

        /// <summary>
        /// Ensure the manager survives scene transitions so it can receive unload events.
        /// </summary>
        protected override void Awake()
        {
            SetPersistAcrossScenes(true);
            base.Awake();

            if (Instance != this)
                return;

            SceneManager.sceneUnloaded += OnSceneUnloaded;
            EnsureDefaultSceneScopeForActiveScene();
        }

        protected override void OnDestroy()
        {
            if (Instance == this)
                SceneManager.sceneUnloaded -= OnSceneUnloaded;

            base.OnDestroy();
        }

        private void OnApplicationQuit()
        {
            // Avoid starting new coroutines during shutdown.
            _flushCoroutine = null;
        }

        public static void BeginScene(string sceneProjectPath)
        {
            var mgr = GetOrCreateInstance();
            mgr.BeginSceneInternal(sceneProjectPath);
        }

        public static void RegisterLoadedPath(string normalizedAssetProjectPath)
        {
            var mgr = GetOrCreateInstance();
            mgr.RegisterLoadedPathInternal(normalizedAssetProjectPath);
        }

        private static AssetScopeManager GetOrCreateInstance()
        {
            if (Instance != null)
                return Instance;

            var go = new GameObject("[AssetScopeManager]");
            go.hideFlags = HideFlags.HideAndDontSave;
            return go.AddComponent<AssetScopeManager>();
        }

        private void EnsureDefaultSceneScopeForActiveScene()
        {
            if (_scopeStack.Count > 0)
                return;

            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid())
            {
                // Scope for the currently active scene so "current scene" loads (e.g. UI) are released when that scene unloads.
                var sceneProjectPath = SceneProjectPathHelper.ToProjectPath(activeScene.name);
                _scopeStack.Add(new AssetScope(sceneProjectPath));
            }
        }

        private void BeginSceneInternal(string sceneProjectPath)
        {
            var path = GameAssetManager.NormalizeAssetPath(sceneProjectPath);
            if (string.IsNullOrEmpty(path))
                return;

            _scopeStack.Add(new AssetScope(path));
        }

        private void RegisterLoadedPathInternal(string normalizedAssetProjectPath)
        {
            var normalized = GameAssetManager.NormalizeAssetPath(normalizedAssetProjectPath);
            if (string.IsNullOrEmpty(normalized))
                return;

            EnsureDefaultSceneScopeForActiveScene();
            if (_scopeStack.Count == 0)
                return;

            _scopeStack[_scopeStack.Count - 1].RegisterLoadedPath(normalized);
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (_scopeStack.Count == 0)
                return;

            var unloadedSceneName = scene.name;

            // Find the newest matching scope first.
            var matchIndex = -1;
            for (var i = _scopeStack.Count - 1; i >= 0; i--)
            {
                var scope = _scopeStack[i];
                var scopeSceneName = GameAssetManager.GetSceneNameForLoad(scope.SceneProjectPath);
                if (string.Equals(scopeSceneName, unloadedSceneName, System.StringComparison.Ordinal))
                {
                    matchIndex = i;
                    break;
                }
            }

            if (matchIndex < 0)
                return;

            // Pop from top down to ensure we don't leak scopes above the matching one.
            while (_scopeStack.Count - 1 >= matchIndex && _scopeStack.Count > 0)
            {
                EndTopScopeInternal();
            }

            EnsureDefaultSceneScopeForActiveScene();
        }

        private void EndTopScopeInternal()
        {
            var topIndex = _scopeStack.Count - 1;
            var scope = _scopeStack[topIndex];
            _scopeStack.RemoveAt(topIndex);

            var releaseCounts = scope.ConsumeReleaseCounts();
            _deferredQueue.EnqueueReleaseCounts(releaseCounts);
            ScheduleFlushNextFrame();
        }

        private void ScheduleFlushNextFrame()
        {
            if (_flushCoroutine != null)
                return;

            _flushCoroutine = StartCoroutine(CoFlushNextFrame());
        }

        private IEnumerator CoFlushNextFrame()
        {
            yield return new WaitForEndOfFrame();

            _flushCoroutine = null;
            FlushNow();
        }

        private void FlushNow()
        {
            var mgr = GameAssetManager.Instance;
            if (mgr == null || !mgr.IsInitialized)
                return;

            if (!_deferredQueue.HasAny)
                return;

            var releaseCounts = _deferredQueue.Drain();
            foreach (var kv in releaseCounts)
            {
                if (kv.Value <= 0)
                    continue;

                for (var i = 0; i < kv.Value; i++)
                    mgr.Release(kv.Key);
            }
        }
    }
}

