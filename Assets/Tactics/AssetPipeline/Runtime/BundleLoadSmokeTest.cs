using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Tactics.AssetPipeline
{
    /// <summary>
    /// Optional smoke test: add to a scene, run <c>Tactics &gt; Asset Pipeline &gt; Setup Sample</c>, then
    /// <c>Build Game Asset Bundles</c> with the build config asset selected. Enter Play Mode.
    /// </summary>
    public sealed class BundleLoadSmokeTest : MonoBehaviour
    {
        [SerializeField]
        private string _assetPath = "Assets/Tactics/AssetPipeline/Sample/BundleTestCube.prefab";

        [SerializeField]
        private bool _releaseAfterSpawn = true;

        private async void Start()
        {
            if (!GameAssets.Initialize())
                return;

            try
            {
                var prefab = await GameAsset.LoadAsync<GameObject>(_assetPath);
                Instantiate(prefab);
                if (_releaseAfterSpawn)
                    GameAsset.Release(_assetPath);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
