using System.Collections.Generic;

namespace Tactics.AssetPipeline
{
    /// <summary>
    /// Tracks asset loads within a logical lifetime scope.
    /// When the scope ends, recorded loads are converted into deferred release counts.
    /// </summary>
    internal sealed class AssetScope
    {
        public string SceneProjectPath { get; }

        private readonly Dictionary<string, int> _assetPathToRetainCount =
            new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);

        public AssetScope(string sceneProjectPath)
        {
            SceneProjectPath = sceneProjectPath;
        }

        public bool HasAny => _assetPathToRetainCount.Count > 0;

        public void RegisterLoadedPath(string normalizedAssetProjectPath)
        {
            if (string.IsNullOrEmpty(normalizedAssetProjectPath))
                return;

            if (_assetPathToRetainCount.TryGetValue(normalizedAssetProjectPath, out var c))
                _assetPathToRetainCount[normalizedAssetProjectPath] = c + 1;
            else
                _assetPathToRetainCount[normalizedAssetProjectPath] = 1;
        }

        public Dictionary<string, int> ConsumeReleaseCounts()
        {
            // Snapshot and clear so the scope can be safely ended only once.
            var snapshot = new Dictionary<string, int>(_assetPathToRetainCount, System.StringComparer.OrdinalIgnoreCase);
            _assetPathToRetainCount.Clear();
            return snapshot;
        }
    }
}

