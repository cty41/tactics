using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Tactics.AssetPipeline;
using Tactics.Common.AI.MonsterAI;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using Tactics.Roguelike;
using Tactics.RoguelikeMap;
using Tactics.Runtime.Utilities;
using Tactics.Units;
using UnityEngine;

namespace Tactics.Common.Battle
{
    [Serializable]
    public sealed class EncounterConfig
    {
        [JsonProperty("encounterId")]
        public string EncounterId;

        [JsonProperty("recipeId")]
        public string RecipeId;

        [JsonProperty("runSeed")]
        public int? RunSeed;

        [JsonProperty("units")]
        public List<EncounterUnitEntry> Units = new List<EncounterUnitEntry>();

        [JsonProperty("blockedCells")]
        public List<BattleLayoutCell> BlockedCells = new List<BattleLayoutCell>();
    }

    [Serializable]
    public sealed class EncounterUnitEntry
    {
        [JsonProperty("monsterId")]
        public string MonsterId;

        [JsonProperty("unitName")]
        public string UnitName;

        [JsonProperty("unitPrefabPath")]
        public string UnitPrefabPath;

        [JsonProperty("aiBrainAssetPath")]
        public string AiBrainAssetPath;

        [JsonProperty("abilityConfigPaths")]
        public List<string> AbilityConfigPaths = new List<string>();

        [JsonProperty("playerNumber")]
        public int PlayerNumber = 1;

        [JsonProperty("spawnCellX")]
        public int SpawnCellX;

        [JsonProperty("spawnCellY")]
        public int SpawnCellY;

        [JsonProperty("healthMultiplier")]
        public float HealthMultiplier = 1f;

        [JsonProperty("outputMultiplier")]
        public float OutputMultiplier = 1f;

        [JsonProperty("minimumStartingMana")]
        public int MinimumStartingMana;
    }

    /// <summary>
    /// Identifies the runtime assets used by one monster archetype.
    /// </summary>
    /// <remarks>
    /// This is pure encounter data. Combat behavior remains owned by the referenced ability and brain assets.
    /// </remarks>
    [Serializable]
    public sealed class MonsterDefinition
    {
        public string MonsterId { get; set; }
        public string DisplayName { get; set; }
        public string UnitPrefabPath { get; set; }
        public string AiBrainAssetPath { get; set; }
        public List<string> AbilityConfigPaths { get; set; } = new List<string>();
        public int MinimumStartingMana { get; set; }
    }

    /// <summary>
    /// Stores one board coordinate used by a battle layout.
    /// </summary>
    [Serializable]
    public sealed class BattleLayoutCell
    {
        public BattleLayoutCell()
        {
        }

        public BattleLayoutCell(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; set; }
        public int Y { get; set; }
    }

    /// <summary>
    /// Defines deterministic spawn slots and static blocker cells for an encounter.
    /// </summary>
    [Serializable]
    public sealed class BattleLayout
    {
        public string LayoutId { get; set; }
        public List<BattleLayoutCell> SpawnCells { get; set; } = new List<BattleLayoutCell>();
        public List<BattleLayoutCell> BlockedCells { get; set; } = new List<BattleLayoutCell>();
    }

    /// <summary>
    /// Defines one exact monster composition option for a recipe.
    /// </summary>
    [Serializable]
    public sealed class EncounterRecipeVariant
    {
        public List<string> MonsterIds { get; set; } = new List<string>();
    }

    /// <summary>
    /// Defines authored encounter composition, layout, and explicit stat multipliers.
    /// </summary>
    /// <remarks>
    /// Recipes are selected directly by id. They intentionally contain no runtime threat budget.
    /// </remarks>
    [Serializable]
    public sealed class EncounterRecipe
    {
        public string RecipeId { get; set; }
        public string LayoutId { get; set; }
        public float HealthMultiplier { get; set; } = 1f;
        public float OutputMultiplier { get; set; } = 1f;
        public List<EncounterRecipeVariant> Variants { get; set; } = new List<EncounterRecipeVariant>();
    }

    /// <summary>
    /// Stores one concrete monster and spawn cell produced by encounter resolution.
    /// </summary>
    [Serializable]
    public sealed class ResolvedEncounterUnit
    {
        public MonsterDefinition Monster { get; set; }
        public BattleLayoutCell SpawnCell { get; set; }
        public float HealthMultiplier { get; set; }
        public float OutputMultiplier { get; set; }
    }

    /// <summary>
    /// Stores the fully deterministic output of resolving an authored encounter recipe.
    /// </summary>
    [Serializable]
    public sealed class ResolvedEncounter
    {
        public string RecipeId { get; set; }
        public int RunSeed { get; set; }
        public BattleLayout Layout { get; set; }
        public float HealthMultiplier { get; set; }
        public float OutputMultiplier { get; set; }
        public List<ResolvedEncounterUnit> Units { get; set; } = new List<ResolvedEncounterUnit>();

        /// <summary>
        /// Converts resolved data to the existing encounter spawn contract.
        /// </summary>
        /// <returns>An encounter config containing concrete runtime asset paths and spawn cells.</returns>
        public EncounterConfig ToEncounterConfig()
        {
            var config = new EncounterConfig
            {
                EncounterId = RecipeId,
                RecipeId = RecipeId,
                RunSeed = RunSeed
            };
            config.BlockedCells = Layout?.BlockedCells == null
                ? new List<BattleLayoutCell>()
                : Layout.BlockedCells.Select(cell => new BattleLayoutCell(cell.X, cell.Y)).ToList();

            foreach (var unit in Units)
            {
                config.Units.Add(new EncounterUnitEntry
                {
                    MonsterId = unit.Monster.MonsterId,
                    UnitName = unit.Monster.DisplayName,
                    UnitPrefabPath = unit.Monster.UnitPrefabPath,
                    AiBrainAssetPath = unit.Monster.AiBrainAssetPath,
                    AbilityConfigPaths = new List<string>(unit.Monster.AbilityConfigPaths),
                    PlayerNumber = 1,
                    SpawnCellX = unit.SpawnCell.X,
                    SpawnCellY = unit.SpawnCell.Y,
                    HealthMultiplier = unit.HealthMultiplier,
                    OutputMultiplier = unit.OutputMultiplier,
                    MinimumStartingMana = unit.Monster.MinimumStartingMana
                });
            }

            return config;
        }
    }

    /// <summary>
    /// Provides the first-slice monster definitions, authored recipes, and battle layouts.
    /// </summary>
    public static class EncounterCatalog
    {
        public const string ChargerId = "charger";
        public const string RangedId = "ranged";
        public const string AoeId = "aoe";
        public const string SupportId = "support";
        public const string EliteChargerId = "elite_charger";
        public const string ElitePoisonCasterId = "elite_poison_caster";

        private const string AiFolder = "Assets/Tactics/AI/Encounters/";
        private const string UnitFolder = "Assets/Tactics/Arts/PureRun/Prefabs/Units/";
        private const string AbilityFolder = "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/";

        private static readonly Dictionary<string, MonsterDefinition> _monsters = BuildMonsters();
        private static readonly Dictionary<string, BattleLayout> _layouts = BuildLayouts();
        private static readonly Dictionary<string, EncounterRecipe> _recipes = BuildRecipes();

        public static IReadOnlyDictionary<string, MonsterDefinition> Monsters => _monsters;
        public static IReadOnlyDictionary<string, BattleLayout> Layouts => _layouts;
        public static IReadOnlyDictionary<string, EncounterRecipe> Recipes => _recipes;

        public static bool TryGetMonster(string monsterId, out MonsterDefinition definition)
        {
            definition = null;
            return !string.IsNullOrWhiteSpace(monsterId) && _monsters.TryGetValue(monsterId, out definition);
        }

        public static bool TryGetLayout(string layoutId, out BattleLayout layout)
        {
            layout = null;
            return !string.IsNullOrWhiteSpace(layoutId) && _layouts.TryGetValue(layoutId, out layout);
        }

        public static bool TryGetRecipe(string recipeId, out EncounterRecipe recipe)
        {
            recipe = null;
            return !string.IsNullOrWhiteSpace(recipeId) && _recipes.TryGetValue(recipeId, out recipe);
        }

        private static Dictionary<string, MonsterDefinition> BuildMonsters()
        {
            return new Dictionary<string, MonsterDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                [ChargerId] = Monster(ChargerId, "Charger", "PureRunGoatCharger.prefab", 0, "MeleeAttack_Graph_Ability.asset", "ChargeStrike_Lv1_Ability.asset"),
                [RangedId] = Monster(RangedId, "Ranged", "PureRunGoatRanged.prefab", 15, "RangedAttack_Graph_Ability.asset", "HeavyShot_Graph_Ability.asset"),
                [AoeId] = Monster(AoeId, "AOE", "PureRunGoatAoe.prefab", 0, "MeleeAttack_Graph_Ability.asset", "AreaBlast_Lv1_Ability.asset"),
                [SupportId] = Monster(SupportId, "Support", "PureRunGoatSupport.prefab", 0, "MeleeAttack_Graph_Ability.asset", "Curse_Graph_Ability.asset"),
                [EliteChargerId] = Monster(EliteChargerId, "EliteCharger", "PureRunGoatEliteCharger.prefab", 0, "MeleeAttack_Graph_Ability.asset", "ChargeStrike_Lv1_Ability.asset"),
                [ElitePoisonCasterId] = Monster(ElitePoisonCasterId, "ElitePoisonCaster", "PureRunGoatElitePoisonCaster.prefab", 0, "MeleeAttack_Graph_Ability.asset", "AreaBlast_Lv1_Ability.asset")
            };
        }

        private static MonsterDefinition Monster(string id, string displayName, string prefabName, int minimumStartingMana, params string[] abilityConfigNames)
        {
            var abilityPaths = new List<string>();
            foreach (string abilityConfigName in abilityConfigNames)
                abilityPaths.Add(AbilityFolder + abilityConfigName);

            return new MonsterDefinition
            {
                MonsterId = id,
                DisplayName = displayName,
                UnitPrefabPath = UnitFolder + prefabName,
                AiBrainAssetPath = AiFolder + displayName + "Brain.asset",
                AbilityConfigPaths = abilityPaths,
                MinimumStartingMana = minimumStartingMana
            };
        }

        private static Dictionary<string, BattleLayout> BuildLayouts()
        {
            return new Dictionary<string, BattleLayout>(StringComparer.OrdinalIgnoreCase)
            {
                ["open"] = Layout("open", new[] { Cell(28, 24), Cell(32, 28), Cell(28, 28), Cell(32, 24) }),
                ["center_blocker"] = Layout(
                    "center_blocker",
                    new[] { Cell(27, 24), Cell(33, 28), Cell(27, 28), Cell(33, 24) },
                    new[] { Cell(30, 26) }),
                ["split_flank"] = Layout(
                    "split_flank",
                    new[] { Cell(26, 24), Cell(34, 28), Cell(26, 28), Cell(34, 24) })
            };
        }

        private static BattleLayout Layout(string id, BattleLayoutCell[] spawnCells, BattleLayoutCell[] blockedCells = null)
        {
            return new BattleLayout
            {
                LayoutId = id,
                SpawnCells = new List<BattleLayoutCell>(spawnCells),
                BlockedCells = blockedCells == null
                    ? new List<BattleLayoutCell>()
                    : new List<BattleLayoutCell>(blockedCells)
            };
        }

        private static BattleLayoutCell Cell(int x, int y)
        {
            return new BattleLayoutCell(x, y);
        }

        private static Dictionary<string, EncounterRecipe> BuildRecipes()
        {
            return new Dictionary<string, EncounterRecipe>(StringComparer.OrdinalIgnoreCase)
            {
                ["N1"] = Recipe("N1", "open", 1f, 1f, Variant(ChargerId, ChargerId, RangedId)),
                ["N2"] = Recipe("N2", "open", 1f, 1f, Variant(RangedId, RangedId, SupportId)),
                ["N3"] = Recipe("N3", "center_blocker", 1f, 1f, Variant(AoeId, ChargerId, ChargerId, SupportId)),
                ["N4"] = Recipe("N4", "split_flank", 1f, 1f, Variant(RangedId, RangedId, AoeId, ChargerId)),
                ["N5"] = Recipe("N5", "center_blocker", 1f, 1f, Variant(SupportId, SupportId, ChargerId, AoeId)),
                ["N6"] = Recipe("N6", "split_flank", 1f, 1f, Variant(ChargerId, ChargerId, RangedId, AoeId)),
                ["E1"] = Recipe("E1", "center_blocker", 1.3f, 1.15f, Variant(AoeId, ChargerId, ChargerId, SupportId)),
                ["E2"] = Recipe("E2", "split_flank", 1.3f, 1.15f, Variant(RangedId, RangedId, AoeId, ChargerId)),
                ["Special"] = Recipe(
                    "Special",
                    "open",
                    1.8f,
                    1.25f,
                    Variant(EliteChargerId),
                    Variant(ElitePoisonCasterId))
            };
        }

        private static EncounterRecipe Recipe(
            string id,
            string layoutId,
            float healthMultiplier,
            float outputMultiplier,
            params EncounterRecipeVariant[] variants)
        {
            return new EncounterRecipe
            {
                RecipeId = id,
                LayoutId = layoutId,
                HealthMultiplier = healthMultiplier,
                OutputMultiplier = outputMultiplier,
                Variants = new List<EncounterRecipeVariant>(variants)
            };
        }

        private static EncounterRecipeVariant Variant(params string[] monsterIds)
        {
            return new EncounterRecipeVariant
            {
                MonsterIds = new List<string>(monsterIds)
            };
        }
    }

    /// <summary>
    /// Resolves an authored encounter recipe into concrete monsters and spawn cells.
    /// </summary>
    /// <remarks>
    /// Resolution is deterministic for a recipe id and run seed. It never derives or compares threat values.
    /// </remarks>
    public static class EncounterResolver
    {
        public static bool TryResolve(string recipeId, int runSeed, out ResolvedEncounter resolved, out string error)
        {
            resolved = null;
            error = null;

            if (!EncounterCatalog.TryGetRecipe(recipeId, out var recipe))
            {
                error = $"Unknown encounter recipe '{recipeId}'.";
                return false;
            }

            if (!EncounterCatalog.TryGetLayout(recipe.LayoutId, out var layout))
            {
                error = $"Encounter recipe '{recipeId}' references unknown layout '{recipe.LayoutId}'.";
                return false;
            }

            if (recipe.Variants == null || recipe.Variants.Count == 0)
            {
                error = $"Encounter recipe '{recipeId}' has no composition variants.";
                return false;
            }

            int variantIndex = GetStableVariantIndex(recipe.RecipeId, runSeed, recipe.Variants.Count);
            var variant = recipe.Variants[variantIndex];
            if (variant?.MonsterIds == null || variant.MonsterIds.Count == 0)
            {
                error = $"Encounter recipe '{recipeId}' selected an empty composition variant.";
                return false;
            }

            if (layout.SpawnCells == null || layout.SpawnCells.Count < variant.MonsterIds.Count)
            {
                error = $"Battle layout '{layout.LayoutId}' has fewer spawn cells than recipe '{recipeId}' requires.";
                return false;
            }

            resolved = new ResolvedEncounter
            {
                RecipeId = recipe.RecipeId,
                RunSeed = runSeed,
                Layout = layout,
                HealthMultiplier = recipe.HealthMultiplier,
                OutputMultiplier = recipe.OutputMultiplier
            };

            for (int i = 0; i < variant.MonsterIds.Count; i++)
            {
                string monsterId = variant.MonsterIds[i];
                if (!EncounterCatalog.TryGetMonster(monsterId, out var monster))
                {
                    resolved = null;
                    error = $"Encounter recipe '{recipeId}' references unknown monster '{monsterId}'.";
                    return false;
                }

                resolved.Units.Add(new ResolvedEncounterUnit
                {
                    Monster = monster,
                    SpawnCell = layout.SpawnCells[i],
                    HealthMultiplier = recipe.HealthMultiplier,
                    OutputMultiplier = recipe.OutputMultiplier
                });
            }

            return true;
        }

        public static ResolvedEncounter Resolve(string recipeId, int runSeed)
        {
            if (!TryResolve(recipeId, runSeed, out var resolved, out var error))
                throw new InvalidOperationException(error);

            return resolved;
        }

        private static int GetStableVariantIndex(string recipeId, int runSeed, int variantCount)
        {
            unchecked
            {
                uint hash = 2166136261;
                string input = $"{recipeId}:{runSeed}";
                for (int i = 0; i < input.Length; i++)
                {
                    hash ^= input[i];
                    hash *= 16777619;
                }

                return (int)(hash % (uint)variantCount);
            }
        }
    }

    public static class EncounterRuntimeState
    {
        public const string PendingEncounterPrefsKey = "RoguelikePendingEncounter";
        public const string PendingEncounterRecipePrefsKey = "RoguelikePendingEncounterRecipe";
        public const string PendingEncounterSeedPrefsKey = "RoguelikePendingEncounterSeed";

        public static void SetPendingEncounterPath(string encounterPath)
        {
            var normalized = EncounterConfigLoader.NormalizeEncounterPath(encounterPath);
            PlayerPrefs.SetString(PendingEncounterPrefsKey, normalized);
            PlayerPrefs.DeleteKey(PendingEncounterRecipePrefsKey);
            PlayerPrefs.DeleteKey(PendingEncounterSeedPrefsKey);
            PlayerPrefs.Save();
        }

        public static void SetPendingEncounterRecipe(string recipeId, int runSeed)
        {
            if (!EncounterConfigLoader.IsEncounterRecipeId(recipeId))
            {
                TLog.Warning($"[EncounterRuntimeState] Ignored unknown encounter recipe '{recipeId}'.");
                return;
            }

            PlayerPrefs.SetString(PendingEncounterRecipePrefsKey, recipeId);
            PlayerPrefs.SetInt(PendingEncounterSeedPrefsKey, runSeed);
            PlayerPrefs.DeleteKey(PendingEncounterPrefsKey);
            PlayerPrefs.Save();
        }

        public static void SetPendingEncounter(string encounterPathOrRecipeId, int runSeed)
        {
            if (EncounterConfigLoader.IsEncounterRecipeId(encounterPathOrRecipeId))
                SetPendingEncounterRecipe(encounterPathOrRecipeId, runSeed);
            else
                SetPendingEncounterPath(encounterPathOrRecipeId);
        }

        public static string GetPendingEncounterPath()
        {
            return EncounterConfigLoader.NormalizeEncounterPath(
                PlayerPrefs.GetString(PendingEncounterPrefsKey, EncounterConfigLoader.DefaultMinorEnemyEncounterPath));
        }

        public static bool TryResolvePendingEncounter(GameAssetManager manager, out EncounterConfig encounter, out string source)
        {
            encounter = null;
            source = GetPendingEncounterPath();
            string recipeId = PlayerPrefs.GetString(PendingEncounterRecipePrefsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(recipeId))
                return false;

            int runSeed = PlayerPrefs.GetInt(PendingEncounterSeedPrefsKey, 0);
            source = $"recipe:{recipeId}@{runSeed}";
            if (!EncounterResolver.TryResolve(recipeId, runSeed, out var resolved, out var error))
            {
                TLog.Error($"[EncounterRuntimeState] Failed to resolve {source}: {error}");
                return false;
            }

            encounter = resolved.ToEncounterConfig();
            return EncounterConfigLoader.ValidateForRuntime(encounter, source, manager);
        }

        public static void ClearPendingEncounter()
        {
            PlayerPrefs.DeleteKey(PendingEncounterPrefsKey);
            PlayerPrefs.DeleteKey(PendingEncounterRecipePrefsKey);
            PlayerPrefs.DeleteKey(PendingEncounterSeedPrefsKey);
            PlayerPrefs.Save();
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

        public static string GetPureRunEncounterRecipeId(RoguelikeNodeType nodeType, int runSeed, string nodeId)
        {
            string[] pool = nodeType switch
            {
                RoguelikeNodeType.MinorEnemy => new[] { "N1", "N2", "N3", "N4", "N5", "N6" },
                RoguelikeNodeType.EliteEnemy => new[] { "E1", "E2" },
                RoguelikeNodeType.Boss => new[] { "Special" },
                _ => null
            };

            if (pool == null)
                return string.Empty;

            uint index = (uint)RoguelikeMapRuntimeState.DeriveSeed(
                runSeed,
                $"pure-run-encounter:{nodeId ?? string.Empty}");
            return pool[index % (uint)pool.Length];
        }

        public static bool IsEncounterRecipeId(string recipeId)
        {
            return !string.IsNullOrWhiteSpace(recipeId) && EncounterCatalog.TryGetRecipe(recipeId, out _);
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
                if ((config?.Units == null || config.Units.Count == 0) && !string.IsNullOrWhiteSpace(config?.RecipeId))
                {
                    if (!EncounterResolver.TryResolve(config.RecipeId, config.RunSeed ?? 0, out var resolved, out var error))
                    {
                        TLog.Error($"[EncounterConfigLoader] Failed to resolve encounter recipe in '{normalizedPath}': {error}");
                        return null;
                    }

                    config = resolved.ToEncounterConfig();
                }

                if (!Validate(config, normalizedPath) || !ValidateRuntimeAssets(config, normalizedPath, mgr))
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

        public static bool ValidateForRuntime(EncounterConfig config, string sourcePath, GameAssetManager manager)
        {
            return Validate(config, sourcePath) && ValidateRuntimeAssets(config, sourcePath, manager);
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

                if (string.IsNullOrWhiteSpace(unit.AiBrainAssetPath))
                {
                    TLog.Error($"[EncounterConfigLoader] Encounter unit #{i} is missing aiBrainAssetPath: {sourcePath}");
                    return false;
                }

                if (!float.IsFinite(unit.HealthMultiplier) || unit.HealthMultiplier <= 0f ||
                    !float.IsFinite(unit.OutputMultiplier) || unit.OutputMultiplier <= 0f)
                {
                    TLog.Error($"[EncounterConfigLoader] Encounter unit #{i} has invalid multipliers: {sourcePath}");
                    return false;
                }

                var cellKey = $"{unit.SpawnCellX},{unit.SpawnCellY}";
                if (!occupiedCells.Add(cellKey))
                {
                    TLog.Error($"[EncounterConfigLoader] Duplicate spawn cell '{cellKey}' in encounter: {sourcePath}");
                    return false;
                }
            }

            if (config.BlockedCells != null)
            {
                foreach (var blocked in config.BlockedCells)
                {
                    if (blocked == null)
                    {
                        TLog.Error($"[EncounterConfigLoader] Encounter has a null blocked cell: {sourcePath}");
                        return false;
                    }

                    string blockedKey = $"{blocked.X},{blocked.Y}";
                    if (occupiedCells.Contains(blockedKey))
                    {
                        TLog.Error($"[EncounterConfigLoader] Blocked cell overlaps spawn cell '{blockedKey}': {sourcePath}");
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool ValidateRuntimeAssets(EncounterConfig config, string sourcePath, GameAssetManager manager)
        {
            var loadedPaths = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                foreach (var unit in config.Units)
                {
                    string prefabPath = GameAssetManager.NormalizeAssetPath(unit.UnitPrefabPath);
                    var prefab = manager.Load<GameObject>(prefabPath);
                    loadedPaths.Add(prefabPath);
                    var prefabUnit = prefab?.GetComponent<TilemapUnit>();
                    if (prefabUnit == null)
                    {
                        TLog.Error($"[EncounterConfigLoader] Unit prefab is missing TilemapUnit: {prefabPath}");
                        return false;
                    }

                    string brainPath = GameAssetManager.NormalizeAssetPath(unit.AiBrainAssetPath);
                    var brain = manager.Load<AiBrainAsset>(brainPath);
                    loadedPaths.Add(brainPath);
                    if (brain == null || !brain.IsValid() || brain.Profile == null)
                    {
                        TLog.Error($"[EncounterConfigLoader] AI brain/profile is invalid: {brainPath}");
                        return false;
                    }

                    var configuredAbilityNames = new HashSet<string>(StringComparer.Ordinal);
                    int maximumManaCost = 0;
                    foreach (string configuredPath in unit.AbilityConfigPaths ?? new List<string>())
                    {
                        string abilityPath = GameAssetManager.NormalizeAssetPath(configuredPath);
                        var ability = manager.Load<AbilityConfig>(abilityPath);
                        loadedPaths.Add(abilityPath);
                        if (ability == null)
                        {
                            TLog.Error($"[EncounterConfigLoader] Ability config is missing: {abilityPath}");
                            return false;
                        }

                        configuredAbilityNames.Add(ability.DisplayName);
                        maximumManaCost = Math.Max(maximumManaCost, ability.ManaCost);
                    }

                    int initialMana = Math.Max(prefabUnit.Charisma, unit.MinimumStartingMana);
                    if (initialMana < maximumManaCost)
                    {
                        TLog.Error($"[EncounterConfigLoader] Monster '{unit.MonsterId}' starts with {initialMana} MP but requires {maximumManaCost}: {sourcePath}");
                        return false;
                    }

                    foreach (var step in brain.PatternSteps)
                    {
                        if (step == null || string.IsNullOrWhiteSpace(step.AbilityName) ||
                            !configuredAbilityNames.Contains(step.AbilityName))
                        {
                            TLog.Error($"[EncounterConfigLoader] Brain '{brainPath}' pattern references an unavailable ability '{step?.AbilityName}'.");
                            return false;
                        }
                    }
                }

                return true;
            }
            finally
            {
                foreach (string path in loadedPaths)
                    manager.Release(path);
            }
        }
    }
}
