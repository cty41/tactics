using System;
using UnityEngine;

namespace Tactics.AssetPipeline
{
    /// <summary>
    /// Optional smoke test: add <see cref="GameAssetManager"/> prefab to the scene, run
    /// <c>Tactics &gt; Asset Pipeline &gt; Setup Sample</c>, then <c>Build Game Asset Bundles</c>.
    /// Enter Play Mode.
    /// </summary>
    public sealed class BundleLoadSmokeTest : MonoBehaviour
    {
        [SerializeField]
        private string _assetPath = "Assets/Tactics/AssetPipeline/Sample/BundleTestCube.prefab";

        [SerializeField]
        private bool _releaseAfterSpawn = true;

        private async void Start()
        {
            var mgr = GameAssetManager.Instance;
            if (mgr == null)
            {
                Debug.LogError("[BundleLoadSmokeTest] No GameAssetManager in scene. Add Assets/Tactics/AssetPipeline/GameAssetManager.prefab.");
                return;
            }

            if (!mgr.IsInitialized && !mgr.Initialize())
                return;

            try
            {
                var prefab = await mgr.LoadAsync<GameObject>(_assetPath);
                Instantiate(prefab);
                if (_releaseAfterSpawn)
                    mgr.Release(_assetPath);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
