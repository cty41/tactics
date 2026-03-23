using UnityEngine;

namespace Tactics.AssetPipeline
{
    /// <summary>
    /// Shared runtime options for <see cref="GameAssetManager"/>. Reference from bootstrap (e.g. <c>GameMain</c>) and call <see cref="GameAssetManager.ApplyRuntimeSettings"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "GameAssetRuntimeSettings", menuName = "Tactics/Asset Pipeline/Runtime Settings", order = 1)]
    public sealed class GameAssetRuntimeSettings : ScriptableObject
    {
        [Tooltip("StreamingBundles: load AssetBundles + manifest. EditorAssetDatabase: Editor Play Mode only, AssetDatabase paths, no manifest/bundles.")]
        public GameAssetLoadMode loadMode = GameAssetLoadMode.StreamingBundles;

        [Tooltip("Empty = StreamingAssets/Bundles. Otherwise absolute path to bundle output folder containing manifest.json.")]
        public string bundlesRootOverride = "";

        [Tooltip("If true, GameAssetManager initializes in Awake. If false, bootstrap (e.g. GameMain) should call Initialize/InitializeAsync.")]
        public bool autoInitializeOnAwake;

        [Tooltip("Keep the manager alive across scene loads (DontDestroyOnLoad).")]
        public bool persistAcrossScenes = true;
    }
}
