using System.IO;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Tactics.AssetPipeline.Editor
{
    public sealed class GameAssetPipelineWindow : OdinEditorWindow
    {
        private const string PrefPrefix = "Tactics.AssetPipeline.";

        public static void Open()
        {
            GetWindow<GameAssetPipelineWindow>("Asset Pipeline").Show();
        }

        [BoxGroup("Config")]
        [Tooltip("If null, uses FindDefault() or selection when running from menu.")]
        public GameAssetBuildConfig buildConfig;

        [BoxGroup("Bundle build")]
        [FolderPath(AbsolutePath = true)]
        [Tooltip(
            "Intermediate AssetBundle output root; platform folder is appended (e.g. .../StandaloneWindows64). " +
            "The player loads bundles from StreamingAssets after this step copies them there—not from Output.")]
        public string bundleBuildRoot = "";

        [BoxGroup("Bundle build")]
        public BuildTarget bundleBuildTarget;

        [BoxGroup("Streaming")]
        [FolderPath(AbsolutePath = true)]
        [Tooltip("Leave empty to copy bundles to Assets/StreamingAssets/{subfolder} from config.")]
        public string streamingBundlesDestinationOverride = "";

        [BoxGroup("Player")]
        public BuildTarget playerBuildTarget;

        [BoxGroup("Player")]
        [FolderPath(AbsolutePath = true)]
        public string playerOutputFolder = "";

        [BoxGroup("Player")]
        public bool buildBundlesBeforePlayer;

        [BoxGroup("Player")]
        public bool developmentBuild;

        protected override void OnEnable()
        {
            base.OnEnable();
            LoadPrefs();
            if (buildConfig == null)
                buildConfig = GameAssetBuildConfig.FindDefault();
            if (string.IsNullOrEmpty(bundleBuildRoot))
                bundleBuildRoot = BuildOutputLayout.GetDefaultBundleBuildRoot();
            if (string.IsNullOrEmpty(playerOutputFolder))
                playerOutputFolder = BuildOutputLayout.GetDefaultPlayerOutputFolder();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            SavePrefs();
        }

        private void LoadPrefs()
        {
            bundleBuildRoot = EditorPrefs.GetString(PrefPrefix + "BundleBuildRoot", "");
            streamingBundlesDestinationOverride = EditorPrefs.GetString(PrefPrefix + "StreamingOverride", "");
            playerOutputFolder = EditorPrefs.GetString(PrefPrefix + "PlayerOut", "");
            bundleBuildTarget = (BuildTarget)EditorPrefs.GetInt(PrefPrefix + "BundleTarget",
                (int)EditorUserBuildSettings.activeBuildTarget);
            playerBuildTarget = (BuildTarget)EditorPrefs.GetInt(PrefPrefix + "PlayerTarget",
                (int)EditorUserBuildSettings.activeBuildTarget);
            buildBundlesBeforePlayer = EditorPrefs.GetInt(PrefPrefix + "BundlesFirst", 0) != 0;
            developmentBuild = EditorPrefs.GetInt(PrefPrefix + "DevBuild", 0) != 0;
            var guid = EditorPrefs.GetString(PrefPrefix + "ConfigGuid", "");
            if (!string.IsNullOrEmpty(guid))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                    buildConfig = AssetDatabase.LoadAssetAtPath<GameAssetBuildConfig>(path);
            }
        }

        private void SavePrefs()
        {
            EditorPrefs.SetString(PrefPrefix + "BundleBuildRoot", bundleBuildRoot ?? "");
            EditorPrefs.SetString(PrefPrefix + "StreamingOverride", streamingBundlesDestinationOverride ?? "");
            EditorPrefs.SetString(PrefPrefix + "PlayerOut", playerOutputFolder ?? "");
            EditorPrefs.SetInt(PrefPrefix + "BundleTarget", (int)bundleBuildTarget);
            EditorPrefs.SetInt(PrefPrefix + "PlayerTarget", (int)playerBuildTarget);
            EditorPrefs.SetInt(PrefPrefix + "BundlesFirst", buildBundlesBeforePlayer ? 1 : 0);
            EditorPrefs.SetInt(PrefPrefix + "DevBuild", developmentBuild ? 1 : 0);
            if (buildConfig != null)
            {
                var path = AssetDatabase.GetAssetPath(buildConfig);
                if (!string.IsNullOrEmpty(path))
                    EditorPrefs.SetString(PrefPrefix + "ConfigGuid", AssetDatabase.AssetPathToGUID(path));
            }
        }

        private GameAssetBuildConfig ResolveConfig()
        {
            if (buildConfig != null)
                return buildConfig;
            foreach (var obj in Selection.objects)
            {
                if (obj is GameAssetBuildConfig c)
                    return c;
            }

            return GameAssetBuildConfig.FindDefault();
        }

        [BoxGroup("Actions")]
        [Button(ButtonSizes.Medium)]
        private void BuildBundles()
        {
            RunBuildBundles(false);
        }

        [BoxGroup("Actions")]
        [Button(ButtonSizes.Medium)]
        private void ClearAndBuildBundles()
        {
            RunBuildBundles(true);
        }

        private void RunBuildBundles(bool clear)
        {
            var cfg = ResolveConfig();
            if (cfg == null)
            {
                EditorUtility.DisplayDialog("Game Asset Pipeline",
                    "Assign a GameAssetBuildConfig or create one via Assets > Create > Tactics > Asset Pipeline > Build Config.",
                    "OK");
                return;
            }

            var streamOverride = string.IsNullOrWhiteSpace(streamingBundlesDestinationOverride)
                ? null
                : streamingBundlesDestinationOverride.Trim();
            GameAssetBundleBuilder.Build(cfg, clear, bundleBuildTarget,
                string.IsNullOrWhiteSpace(bundleBuildRoot) ? null : bundleBuildRoot.Trim(), streamOverride);
        }

        [BoxGroup("Actions")]
        [Button(ButtonSizes.Medium)]
        private void BuildPlayer()
        {
            if (buildBundlesBeforePlayer)
            {
                var cfg = ResolveConfig();
                if (cfg == null)
                {
                    EditorUtility.DisplayDialog("Game Asset Pipeline",
                        "Assign a GameAssetBuildConfig when using Build Bundles Before Player.",
                        "OK");
                    return;
                }

                var streamOverride = string.IsNullOrWhiteSpace(streamingBundlesDestinationOverride)
                    ? null
                    : streamingBundlesDestinationOverride.Trim();
                var ok = GameAssetBundleBuilder.Build(cfg, false, bundleBuildTarget,
                    string.IsNullOrWhiteSpace(bundleBuildRoot) ? null : bundleBuildRoot.Trim(), streamOverride);
                if (!ok)
                {
                    EditorUtility.DisplayDialog("Game Asset Pipeline", "Bundle build failed; player build cancelled.", "OK");
                    return;
                }
            }

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                EditorUtility.DisplayDialog("Game Asset Pipeline", "No enabled scenes in Build Settings.", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(playerOutputFolder))
            {
                EditorUtility.DisplayDialog("Game Asset Pipeline", "Set Player output folder.", "OK");
                return;
            }

            Directory.CreateDirectory(playerOutputFolder);
            var location = GetPlayerLocationPath(playerOutputFolder.Trim(), playerBuildTarget);
            var parent = Path.GetDirectoryName(location);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            var options = BuildOptions.None;
            if (developmentBuild)
                options |= BuildOptions.Development;

            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = location,
                target = playerBuildTarget,
                options = options
            };

            var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[GameAssetPipelineWindow] Player build succeeded: {location}");
            }
            else
            {
                Debug.LogError(
                    $"[GameAssetPipelineWindow] Player build failed: {report.summary.result} — {report.summary.totalErrors} error(s).");
                EditorUtility.DisplayDialog("Game Asset Pipeline",
                    $"Player build failed ({report.summary.result}). See Console for details.", "OK");
            }
        }

        [BoxGroup("Actions")]
        [Button(ButtonSizes.Medium)]
        private void SetupSample()
        {
            GameAssetSampleSetup.Run();
        }

        private static string GetPlayerLocationPath(string outputFolder, BuildTarget target)
        {
            var product = PlayerSettings.productName;
            switch (target)
            {
                case BuildTarget.StandaloneWindows64:
                    return Path.Combine(outputFolder, product + ".exe");
                case BuildTarget.StandaloneOSX:
                    return Path.Combine(outputFolder, product + ".app");
                case BuildTarget.Android:
                    return Path.Combine(outputFolder, product + ".apk");
                default:
                    return Path.Combine(outputFolder, product);
            }
        }
    }
}
