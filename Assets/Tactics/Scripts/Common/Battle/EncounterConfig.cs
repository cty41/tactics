using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Tactics.AssetPipeline;
using Tactics.RoguelikeMap;
using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics.Common.Battle
{
    [Serializable]
    public sealed class EncounterConfig
    {
        [JsonProperty("encounterId")]
        public string EncounterId;

        [JsonProperty("units")]
        public List<EncounterUnitEntry> Units = new List<EncounterUnitEntry>();
    }

    [Serializable]
    public sealed class EncounterUnitEntry
    {
        [JsonProperty("unitName")]
        public string UnitName;

        [JsonProperty("unitPrefabPath")]
        public string UnitPrefabPath;

        [JsonProperty("aiBrainAssetPath")]
        public string AiBrainAssetPath;

        [JsonProperty("playerNumber")]
        public int PlayerNumber = 1;

        [JsonProperty("spawnCellX")]
        public int SpawnCellX;

        [JsonProperty("spawnCellY")]
        public int SpawnCellY;
    }

    public static class EncounterRuntimeState
    {
        public const string PendingEncounterPrefsKey = "RoguelikePendingEncounter";

        public static void SetPendingEncounterPath(string encounterPath)
        {
            var normalized = EncounterConfigLoader.NormalizeEncounterPath(encounterPath);
            PlayerPrefs.SetString(PendingEncounterPrefsKey, normalized);
            PlayerPrefs.Save();
        }

        public static string GetPendingEncounterPath()
        {
            return EncounterConfigLoader.NormalizeEncounterPath(
                PlayerPrefs.GetString(PendingEncounterPrefsKey, EncounterConfigLoader.DefaultMinorEnemyEncounterPath));
        }
    }

    public static class EncounterConfigLoader
    {
        public const string EncountersFolder = "Assets/Tactics/GameData/Encounters/";
        public const string DefaultMinorEnemyEncounterPath = EncountersFolder + "basic_melee.json";

        public static string GetDefaultEncounterPath(RoguelikeNodeType nodeType)
        {
            return nodeType switch
            {
                RoguelikeNodeType.MinorEnemy => DefaultMinorEnemyEncounterPath,
                RoguelikeNodeType.EliteEnemy => DefaultMinorEnemyEncounterPath,
                RoguelikeNodeType.Boss => DefaultMinorEnemyEncounterPath,
                _ => string.Empty
            };
        }

        public static string NormalizeEncounterPath(string encounterPath)
        {
            if (string.IsNullOrWhiteSpace(encounterPath))
                return DefaultMinorEnemyEncounterPath;

            var trimmed = encounterPath.Trim().Replace('\\', '/');
            if (trimmed.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return GameAssetManager.NormalizeAssetPath(trimmed);

            if (trimmed.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                return GameAssetManager.NormalizeAssetPath(EncountersFolder + trimmed);

            return GameAssetManager.NormalizeAssetPath(EncountersFolder + trimmed + ".json");
        }

        public static EncounterConfig Load(string encounterPath, GameAssetManager mgr)
        {
            if (mgr == null)
            {
                TLog.Error("[EncounterConfigLoader] GameAssetManager is null.");
                return null;
            }

            var normalizedPath = NormalizeEncounterPath(encounterPath);
            TextAsset textAsset = null;
            try
            {
                textAsset = mgr.Load<TextAsset>(normalizedPath);
                if (textAsset == null)
                {
                    TLog.Error($"[EncounterConfigLoader] Failed to load encounter file: {normalizedPath}");
                    return null;
                }

                var config = JsonConvert.DeserializeObject<EncounterConfig>(textAsset.text);
                if (!Validate(config, normalizedPath))
                    return null;

                return config;
            }
            catch (Exception ex)
            {
                TLog.Error($"[EncounterConfigLoader] Failed to parse encounter '{normalizedPath}': {ex.Message}");
                return null;
            }
            finally
            {
                if (textAsset != null)
                    mgr.Release(normalizedPath);
            }
        }

        public static bool Validate(EncounterConfig config, string sourcePath)
        {
            if (config == null)
            {
                TLog.Error($"[EncounterConfigLoader] Encounter config is null: {sourcePath}");
                return false;
            }

            if (config.Units == null || config.Units.Count == 0)
            {
                TLog.Error($"[EncounterConfigLoader] Encounter has no units: {sourcePath}");
                return false;
            }

            var occupiedCells = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < config.Units.Count; i++)
            {
                var unit = config.Units[i];
                if (unit == null)
                {
                    TLog.Error($"[EncounterConfigLoader] Encounter unit #{i} is null: {sourcePath}");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(unit.UnitPrefabPath))
                {
                    TLog.Error($"[EncounterConfigLoader] Encounter unit #{i} is missing unitPrefabPath: {sourcePath}");
                    return false;
                }

                var cellKey = $"{unit.SpawnCellX},{unit.SpawnCellY}";
                if (!occupiedCells.Add(cellKey))
                {
                    TLog.Error($"[EncounterConfigLoader] Duplicate spawn cell '{cellKey}' in encounter: {sourcePath}");
                    return false;
                }
            }

            return true;
        }
    }
}
