using System;

namespace Tactics.AssetPipeline
{
    /// <summary>
    /// JSON schema for <see cref="GameAssetPaths.ManifestFileName"/> (JsonUtility-compatible).
    /// </summary>
    [Serializable]
    public class GameAssetManifest
    {
        public int version = 1;
        public BundleRecord[] bundles = Array.Empty<BundleRecord>();
        public AssetRecord[] assets = Array.Empty<AssetRecord>();
    }

    [Serializable]
    public class BundleRecord
    {
        public string name;
        public string file;
        public string hash;
        public long size;
        public string[] deps = Array.Empty<string>();
    }

    [Serializable]
    public class AssetRecord
    {
        public string path;
        public string bundle;
    }
}
