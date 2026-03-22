using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Tactics.AssetPipeline
{
    internal sealed class BundleCache
    {
        private readonly string _bundlesRoot;
        private readonly Dictionary<string, Entry> _loaded = new Dictionary<string, Entry>();

        private sealed class Entry
        {
            public AssetBundle Bundle;
            public int RefCount;
        }

        public BundleCache(string bundlesRoot)
        {
            _bundlesRoot = bundlesRoot;
        }

        public AssetBundle GetLoadedBundle(string bundleName)
        {
            if (_loaded.TryGetValue(bundleName, out var e) && e.Bundle != null)
                return e.Bundle;
            throw new InvalidOperationException($"Bundle not loaded: {bundleName}");
        }

        public bool IsLoaded(string bundleName) =>
            _loaded.TryGetValue(bundleName, out var e) && e.Bundle != null;

        public async Task EnsureLoadedAsync(IReadOnlyList<string> loadOrder)
        {
            foreach (var name in loadOrder)
            {
                if (!_loaded.TryGetValue(name, out var entry))
                    entry = new Entry { RefCount = 0, Bundle = null };

                entry.RefCount++;
                if (entry.Bundle == null)
                {
                    var path = Path.Combine(_bundlesRoot, GameAssets.GetBundleRecord(name).file);
                    var op = AssetBundle.LoadFromFileAsync(path);
                    while (!op.isDone)
                        await Task.Yield();
                    entry.Bundle = op.assetBundle;
                    if (entry.Bundle == null)
                        throw new IOException($"AssetBundle.LoadFromFileAsync failed: {path}");
                }

                _loaded[name] = entry;
            }
        }

        public void EnsureLoadedSync(IReadOnlyList<string> loadOrder)
        {
            foreach (var name in loadOrder)
            {
                if (!_loaded.TryGetValue(name, out var entry))
                    entry = new Entry { RefCount = 0, Bundle = null };

                entry.RefCount++;
                if (entry.Bundle == null)
                {
                    var path = Path.Combine(_bundlesRoot, GameAssets.GetBundleRecord(name).file);
                    entry.Bundle = AssetBundle.LoadFromFile(path);
                    if (entry.Bundle == null)
                        throw new IOException($"AssetBundle.LoadFromFile failed: {path}");
                }

                _loaded[name] = entry;
            }
        }

        public void Release(IReadOnlyList<string> loadOrder)
        {
            for (var i = loadOrder.Count - 1; i >= 0; i--)
            {
                var name = loadOrder[i];
                if (!_loaded.TryGetValue(name, out var entry))
                    continue;
                entry.RefCount--;
                if (entry.RefCount > 0)
                    continue;
                if (entry.Bundle != null)
                {
                    entry.Bundle.Unload(false);
                    entry.Bundle = null;
                }

                _loaded.Remove(name);
            }
        }
    }
}
