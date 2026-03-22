using System;
using System.Collections.Generic;
using Tactics.AssetPipeline;
using UnityEditor;
using UnityEngine;

namespace Tactics.AssetPipeline.Editor
{
    [CreateAssetMenu(fileName = "GameAssetBuildConfig", menuName = "Tactics/Asset Pipeline/Build Config", order = 0)]
    public sealed class GameAssetBuildConfig : ScriptableObject
    {
        [Tooltip("Subfolder under StreamingAssets (default: Bundles).")]
        public string streamingSubfolder = GameAssetPaths.StreamingBundlesFolder;

        public List<GameAssetBundleGroup> groups = new List<GameAssetBundleGroup>();

        public static GameAssetBuildConfig FindDefault()
        {
            var guids = AssetDatabase.FindAssets("t:GameAssetBuildConfig");
            if (guids.Length == 0)
                return null;
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<GameAssetBuildConfig>(path);
        }
    }

    [Serializable]
    public sealed class GameAssetBundleGroup
    {
        [Tooltip("AssetBundle name (no extension). Must be unique across groups.")]
        public string bundleName;

        [Tooltip("Optional: all assets under this folder are assigned to this bundle.")]
        public DefaultAsset rootFolder;

        [Tooltip("Folders under rootFolder (or anywhere) to skip. E.g. exclude Scenes when root is Tactics.")]
        public List<DefaultAsset> excludeFolders = new List<DefaultAsset>();

        [Tooltip("Single asset path, or glob: folder/* = direct children only; folder/** = recursive. Must start with Assets/.")]
        public List<string> extraAssetPaths = new List<string>();
    }
}
