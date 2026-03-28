using UnityEditor;
using UnityEngine;

namespace Tactics.AssetPipeline.Editor
{
    public static class GameAssetPipelineMenu
    {
        private const string WindowMenu = "Tactics/Asset Pipeline/Asset Pipeline Window";
        private const string BuildMenu = "Tactics/Asset Pipeline/Build Game Asset Bundles";
        private const string RebuildMenu = "Tactics/Asset Pipeline/Clear And Build Game Asset Bundles";

        [MenuItem(WindowMenu, false, 5)]
        public static void OpenPipelineWindow()
        {
            GameAssetPipelineWindow.Open();
        }

        [MenuItem(BuildMenu, false, 10)]
        public static void BuildSelectedOrDefault()
        {
            var config = GetConfigFromSelection();
            if (config == null)
            {
                EditorUtility.DisplayDialog("Game Asset Pipeline",
                    "Select a GameAssetBuildConfig asset or create one via Assets > Create > Tactics > Asset Pipeline > Build Config.",
                    "OK");
                return;
            }

            GameAssetBundleBuilder.Build(config, clearDestination: false);
        }

        [MenuItem(RebuildMenu, false, 11)]
        public static void ClearAndBuild()
        {
            var config = GetConfigFromSelection();
            if (config == null)
            {
                EditorUtility.DisplayDialog("Game Asset Pipeline",
                    "Select a GameAssetBuildConfig asset or create one via Assets > Create > Tactics > Asset Pipeline > Build Config.",
                    "OK");
                return;
            }

            GameAssetBundleBuilder.Build(config, clearDestination: true);
        }

        private static GameAssetBuildConfig GetConfigFromSelection()
        {
            foreach (var obj in Selection.objects)
            {
                if (obj is GameAssetBuildConfig c)
                    return c;
            }

            return GameAssetBuildConfig.FindDefault();
        }
    }
}
