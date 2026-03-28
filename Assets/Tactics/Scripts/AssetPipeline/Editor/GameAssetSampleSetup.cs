using System.IO;
using Tactics.AssetPipeline;
using UnityEditor;
using UnityEngine;

namespace Tactics.AssetPipeline.Editor
{
    public static class GameAssetSampleSetup
    {
        private const string SampleFolder = "Assets/Tactics/AssetPipeline/Sample";
        private const string PrefabPath = SampleFolder + "/BundleTestCube.prefab";
        private const string ConfigPath = "Assets/Tactics/AssetPipeline/GameAssetBuildConfig.asset";

        [MenuItem("Tactics/Asset Pipeline/Setup Sample (Prefab + Build Config)", false, 50)]
        public static void SetupSample()
        {
            Run();
        }

        public static void Run()
        {
            EnsureFolder(SampleFolder);
            EnsureFolder("Assets/StreamingAssets");

            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), PrefabPath)))
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "BundleTestCube";
                PrefabUtility.SaveAsPrefabAsset(cube, PrefabPath);
                Object.DestroyImmediate(cube);
            }

            var sampleFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(SampleFolder);
            GameAssetBuildConfig config;

            if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), ConfigPath)))
            {
                config = AssetDatabase.LoadAssetAtPath<GameAssetBuildConfig>(ConfigPath);
            }
            else
            {
                config = ScriptableObject.CreateInstance<GameAssetBuildConfig>();
                config.streamingSubfolder = GameAssetPaths.StreamingBundlesFolder;
                config.groups.Clear();
                config.groups.Add(new GameAssetBundleGroup
                {
                    bundleName = "sample_bundle",
                    rootFolder = sampleFolder
                });
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = config;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[GameAssetSampleSetup] Created/updated sample prefab and config. Run Build Game Asset Bundles with this config selected.");
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;
            var parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            var name = Path.GetFileName(assetPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
                return;
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
