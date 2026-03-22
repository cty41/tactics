using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tactics.AssetPipeline
{
    /// <summary>
    /// Resolves a short scene name or a full <c>Assets/.../*.unity</c> path for <see cref="GameAssetManager.LoadScene"/>.
    /// </summary>
    public static class SceneProjectPathHelper
    {
        public const string DefaultTacticsScenesFolder = "Assets/Tactics/Scenes/";

        /// <summary>
        /// If <paramref name="sceneNameOrPath"/> is already a project scene asset path, normalizes it.
        /// Otherwise treats it as a file name (no extension) under <see cref="DefaultTacticsScenesFolder"/>.
        /// </summary>
        public static string ToProjectPath(string sceneNameOrPath)
        {
            if (string.IsNullOrWhiteSpace(sceneNameOrPath))
                throw new ArgumentException("Scene name or path is empty.", nameof(sceneNameOrPath));

            var s = sceneNameOrPath.Trim().Replace('\\', '/');
            if (s.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) &&
                s.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return GameAssetManager.NormalizeAssetPath(s);

            var name = s.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
                ? System.IO.Path.GetFileNameWithoutExtension(s)
                : s;
            return GameAssetManager.NormalizeAssetPath($"{DefaultTacticsScenesFolder}{name}.unity");
        }

        /// <summary>
        /// Loads via <see cref="GameAssetManager.LoadScene"/> after <see cref="Initialize"/> if needed.
        /// </summary>
        public static bool TryLoadSceneViaAssetManager(string sceneNameOrPath, LoadSceneMode mode = LoadSceneMode.Single)
        {
            var path = ToProjectPath(sceneNameOrPath);
            var mgr = GameAssetManager.Instance;
            if (mgr == null)
            {
                Debug.LogError(
                    "[SceneProjectPathHelper] No GameAssetManager in scene. Add Assets/Tactics/AssetPipeline/GameAssetManager.prefab.");
                return false;
            }

            if (!mgr.IsInitialized && !mgr.Initialize())
                return false;

            mgr.LoadScene(path, mode);
            return true;
        }
    }
}
