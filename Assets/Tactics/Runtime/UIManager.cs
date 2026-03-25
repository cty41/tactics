using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tactics.AssetPipeline;
using Tactics.UI;
using UnityEngine;

namespace Tactics
{
    public sealed class UIManager : MonoBehaviourSingleton<UIManager>
    {
        public enum UIId
        {
            Menu,
            RoguelikeMap
        }

        [SerializeField] private RectTransform _uiRoot;

        private const string MenuPrefabPath = "Assets/Tactics/UI/Menu.prefab";
        private const string RoguelikeMapPrefabPath = "Assets/Tactics/UI/RoguelikeMap.prefab";

        private readonly Dictionary<UIId, GameObject> _instances = new Dictionary<UIId, GameObject>();
        private readonly Dictionary<UIId, Task<GameObject>> _loadingTasks = new Dictionary<UIId, Task<GameObject>>();

        public Task ShowAsync(UIId id)
        {
            return ShowUIAsync(id, GetPrefabPath(id));
        }

        public void Hide(UIId id)
        {
            if (_instances.TryGetValue(id, out var go) && go != null)
                // UI is intentionally tied to the current scene scope:
                // Hide/Destroy only affects visibility/lifetime of the instantiated GameObject,
                // bundle releases happen when the scene scope ends.
                go.SetActive(false);
        }

        public void Destroy(UIId id)
        {
            if (_instances.TryGetValue(id, out var go) && go != null)
                // Do not call AssetScopeManager.EndScope / GameAssetManager.Release here.
                // The loaded prefab remains retained until the owning scene scope ends.
                UnityEngine.Object.Destroy(go);
            _instances.Remove(id);
        }

        public bool IsVisible(UIId id)
        {
            return _instances.TryGetValue(id, out var go) && go != null && go.activeSelf;
        }

        [Obsolete("Use ShowAsync(UIId.Menu) from a domain coordinator.")]
        public Task ShowMenuAsync() => ShowAsync(UIId.Menu);

        [Obsolete("Use Hide(UIId.Menu) from a domain coordinator.")]
        public void HideMenu() => Hide(UIId.Menu);

        [Obsolete("Use Destroy(UIId.Menu) from a domain coordinator.")]
        public void DestroyMenu() => Destroy(UIId.Menu);

        private static string GetPrefabPath(UIId id)
        {
            return id switch
            {
                UIId.Menu => MenuPrefabPath,
                UIId.RoguelikeMap => RoguelikeMapPrefabPath,
                _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown UIId prefab mapping.")
            };
        }

        private async Task ShowUIAsync(UIId id, string prefabPath)
        {
            if (_instances.TryGetValue(id, out var existing) && existing != null)
            {
                existing.SetActive(true);
                return;
            }

            if (!_loadingTasks.TryGetValue(id, out var loadTask))
            {
                loadTask = LoadAndCreateUiInstanceAsync(id, prefabPath);
                _loadingTasks[id] = loadTask;
            }

            var instance = await loadTask;
            _loadingTasks.Remove(id);

            _instances[id] = instance;
            instance.SetActive(true);
        }

        private async Task<GameObject> LoadAndCreateUiInstanceAsync(UIId id, string prefabPath)
        {
            if (_uiRoot == null)
                throw new InvalidOperationException("[UIManager] _uiRoot is not assigned. Assign it to a RectTransform under the target Canvas.");

            var mgr = GameAssetManager.Instance;
            if (mgr == null)
                throw new InvalidOperationException("[UIManager] GameAssetManager.Instance is null. Ensure bootstrap ran before calling UI methods.");

            if (!mgr.IsInitialized)
                throw new InvalidOperationException("[UIManager] GameAssetManager is not initialized. Call GameAssetManager.Initialize/InitializeAsync before calling UI methods.");

            var prefab = await mgr.LoadAsync<GameObject>(prefabPath);
            if (prefab == null)
                throw new InvalidOperationException($"[UIManager] Failed to load prefab: {prefabPath}");

            var go = Instantiate(prefab, _uiRoot, false);
            go.name = id.ToString();
            EnsureController(id, go);
            go.SetActive(false);
            return go;
        }

        private static void EnsureController(UIId id, GameObject root)
        {
            switch (id)
            {
                case UIId.RoguelikeMap:
                    if (root.GetComponent<RoguelikeMapUIController>() == null)
                        root.AddComponent<RoguelikeMapUIController>();
                    break;
                case UIId.Menu:
                default:
                    break;
            }
        }
    }
}

