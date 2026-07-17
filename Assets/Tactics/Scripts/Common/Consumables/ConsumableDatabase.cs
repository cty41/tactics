using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Tactics.AssetPipeline;
using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics.Consumables
{
    /// <summary>
    /// Loads consumable definitions and weighted acquisition pools.
    /// </summary>
    /// <remarks>
    /// Pool rolls accept an explicit seed. Reopening a node therefore cannot reroll
    /// content when callers derive the seed from the run and node identifiers.
    /// </remarks>
    public static class ConsumableDatabase
    {
        public const string ContentPath = "Assets/Tactics/GameData/Consumables.json";

        private static readonly Dictionary<string, ConsumableDefinition> _definitions =
            new Dictionary<string, ConsumableDefinition>(StringComparer.Ordinal);
        private static readonly Dictionary<string, ConsumablePoolDefinition> _pools =
            new Dictionary<string, ConsumablePoolDefinition>(StringComparer.Ordinal);
        private static bool _isLoaded;

        public static void Load()
        {
            if (_isLoaded)
                return;

            string json = null;
            var assetManager = GameAssetManager.Instance;
            if (assetManager != null && assetManager.IsInitialized)
            {
                var textAsset = assetManager.Load<TextAsset>(ContentPath);
                if (textAsset != null)
                {
                    json = textAsset.text;
                    assetManager.Release(ContentPath);
                }
            }
#if UNITY_EDITOR
            else if (File.Exists(ContentPath))
            {
                json = File.ReadAllText(ContentPath);
            }
#endif

            if (string.IsNullOrWhiteSpace(json))
            {
                TLog.Error($"[ConsumableDatabase] Content not found at {ContentPath}.");
                _isLoaded = true;
                return;
            }

            try
            {
                var content = JsonConvert.DeserializeObject<ConsumableContentFile>(json);
                Register(content);
                TLog.Info($"[ConsumableDatabase] Loaded {_definitions.Count} definitions and {_pools.Count} pools.");
            }
            catch (Exception ex)
            {
                TLog.Error($"[ConsumableDatabase] Failed to parse content: {ex.Message}");
            }
            finally
            {
                _isLoaded = true;
            }
        }

        public static ConsumableDefinition GetById(string id)
        {
            EnsureLoaded();
            return !string.IsNullOrWhiteSpace(id) && _definitions.TryGetValue(id, out var definition)
                ? definition
                : null;
        }

        public static IReadOnlyList<ConsumableDefinition> GetAll()
        {
            EnsureLoaded();
            return _definitions.Values.OrderBy(definition => definition.Id, StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// Formats a newly acquired copy using its display name and charge state.
        /// </summary>
        public static string GetAcquisitionDisplayText(string id)
        {
            var definition = GetById(id);
            if (definition == null)
                return id ?? string.Empty;

            int maxCharges = Math.Max(1, definition.MaxCharges);
            return $"{definition.DisplayName}（{maxCharges}/{maxCharges}）";
        }

        public static ConsumableDefinition Roll(string poolId, int seed)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(poolId) || !_pools.TryGetValue(poolId, out var pool))
                return null;

            var eligible = pool.Entries
                .Where(entry => entry != null && entry.Weight > 0f && _definitions.ContainsKey(entry.ConsumableId))
                .ToList();
            if (eligible.Count == 0)
                return null;

            double totalWeight = eligible.Sum(entry => entry.Weight);
            double roll = new System.Random(seed).NextDouble() * totalWeight;
            foreach (var entry in eligible)
            {
                roll -= entry.Weight;
                if (roll < 0d)
                    return _definitions[entry.ConsumableId];
            }

            return _definitions[eligible[^1].ConsumableId];
        }

        internal static void ResetForTests()
        {
            _definitions.Clear();
            _pools.Clear();
            _isLoaded = false;
        }

        private static void EnsureLoaded()
        {
            if (!_isLoaded)
                Load();
        }

        private static void Register(ConsumableContentFile content)
        {
            _definitions.Clear();
            _pools.Clear();

            foreach (var definition in content?.Definitions ?? Enumerable.Empty<ConsumableDefinition>())
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                    continue;
                definition.MaxCharges = Math.Max(1, definition.MaxCharges);
                definition.Price = Math.Max(0, definition.Price);
                _definitions[definition.Id] = definition;
            }

            foreach (var pool in content?.Pools ?? Enumerable.Empty<ConsumablePoolDefinition>())
            {
                if (pool == null || string.IsNullOrWhiteSpace(pool.Id))
                    continue;
                pool.Entries ??= new List<WeightedConsumableEntry>();
                _pools[pool.Id] = pool;
            }
        }
    }
}
