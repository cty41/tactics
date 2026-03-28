using System.Collections.Generic;

namespace Tactics.AssetPipeline
{
    /// <summary>
    /// Aggregates release counts and releases them on a safe boundary (e.g. next frame).
    /// </summary>
    internal sealed class DeferredReleaseQueue
    {
        private readonly Dictionary<string, int> _scheduledReleaseCounts =
            new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);

        public bool HasAny => _scheduledReleaseCounts.Count > 0;

        public void EnqueueReleaseCounts(IReadOnlyDictionary<string, int> releaseCounts)
        {
            if (releaseCounts == null)
                return;

            foreach (var kv in releaseCounts)
            {
                if (string.IsNullOrEmpty(kv.Key))
                    continue;
                if (kv.Value <= 0)
                    continue;

                if (_scheduledReleaseCounts.TryGetValue(kv.Key, out var existing))
                    _scheduledReleaseCounts[kv.Key] = existing + kv.Value;
                else
                    _scheduledReleaseCounts[kv.Key] = kv.Value;
            }
        }

        public Dictionary<string, int> Drain()
        {
            var snapshot = new Dictionary<string, int>(_scheduledReleaseCounts, System.StringComparer.OrdinalIgnoreCase);
            _scheduledReleaseCounts.Clear();
            return snapshot;
        }
    }
}

