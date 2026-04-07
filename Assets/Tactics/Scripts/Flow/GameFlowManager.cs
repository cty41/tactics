using System;
using System.Threading.Tasks;
using Tactics.AssetPipeline;
using UnityEngine;

namespace Tactics
{
    /// <summary>
    /// Collects boot-time logic previously hosted inside <see cref="GameMain"/> so it can be extended without bloating the entry MonoBehaviour.
    /// Scope: only initializes <see cref="GameAssetManager"/> and loads the first scene.
    /// </summary>
    public sealed class GameFlowManager
    {
        public async Task RunAsync(GameAssetRuntimeSettings runtimeSettings, string firstSceneNameOrPath)
        {
            if (runtimeSettings == null)
            {
                Debug.LogError("[GameFlowManager] Assign Game Asset Runtime Settings (ScriptableObject).");
                return;
            }

            if (GameAssetManager.Instance == null)
                GameAssetManager.CreateBootstrap(runtimeSettings);

            var instance = GameAssetManager.Instance;
            if (instance == null)
            {
                Debug.LogError("[GameFlowManager] GameAssetManager.Instance is still null after bootstrap.");
                return;
            }

            if (!instance.IsInitialized)
            {
                if (!await instance.InitializeAsync())
                {
                    Debug.LogError("[GameFlowManager] GameAssetManager.InitializeAsync failed.");
                    return;
                }
            }

            var path = SceneProjectPathHelper.ToProjectPath(firstSceneNameOrPath);
            try
            {
                await instance.LoadSceneAsync(path);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}

