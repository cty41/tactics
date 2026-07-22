using System;
using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using Tactics.Roster;
using Tactics.Runtime.Utilities;

namespace Tactics.Common.Battle
{
    /// <summary>Result of resolving and injecting one Pure Run character's combat loadout.</summary>
    public sealed class PureRunAbilityBindingResult
    {
        internal readonly List<string> LoadedPathsInternal = new();
        internal readonly List<string> MissingSkillIdsInternal = new();
        internal readonly Dictionary<string, int> ResolvedSkillLevelsInternal = new(StringComparer.Ordinal);

        public IReadOnlyList<string> LoadedPaths => LoadedPathsInternal;
        public IReadOnlyList<string> MissingSkillIds => MissingSkillIdsInternal;
        public IReadOnlyDictionary<string, int> ResolvedSkillLevels => ResolvedSkillLevelsInternal;
    }

    /// <summary>
    /// Converts persisted stable skill ids and levels into the exact AbilityConfig list used by a Pure Run unit.
    /// This must run before Unit.Initialize so prefab-authored career skills cannot leak into Pure Run.
    /// </summary>
    public static class PureRunAbilityBinder
    {
        public static PureRunAbilityBindingResult Bind(
            CharacterDefinition character,
            Unit unit,
            Func<string, AbilityConfig> abilityLoader)
        {
            var result = new PureRunAbilityBindingResult();
            if (character == null || unit == null || abilityLoader == null)
                return result;

            unit.ApplyLearnedSkillLevels(character.LearnedSkills);

            var configs = new List<AbilityConfig>();
            var loadedPaths = new HashSet<string>(StringComparer.Ordinal);
            string baseAttackPath = PureRunAbilityCatalog.GetBaseAttackPath(character.RoleType);
            TryLoad(baseAttackPath, "base.attack", 1, 1, abilityLoader, configs, loadedPaths, result);

            var learnedSkills = character.LearnedSkills ?? new List<CharacterDefinition.LearnedSkill>();
            foreach (var learned in learnedSkills
                         .Where(skill => skill?.SkillType == SkillType.Active)
                         .Concat(learnedSkills.Where(skill => skill?.SkillType == SkillType.ExtraUtility)))
            {
                if (!PureRunAbilityCatalog.TryGet(learned.SkillId, out var definition) ||
                    definition.RoleType != character.RoleType ||
                    !definition.IsBattleVisible ||
                    definition.SkillType == SkillType.Passive)
                {
                    continue;
                }

                if (!PureRunAbilityCatalog.TryResolveAbilityPath(
                        learned.SkillId,
                        learned.Level,
                        out string path,
                        out int resolvedLevel))
                {
                    result.MissingSkillIdsInternal.Add(learned.SkillId);
                    // The pickup action is persisted in Slice 1, while its spear-state runtime is delivered in Slice 5.
                    if (definition.SkillType != SkillType.ExtraUtility)
                        TLog.Error($"[PureRunAbilityBinder] No published AbilityConfig for {learned.SkillId} Lv{learned.Level}.");
                    continue;
                }

                if (resolvedLevel != learned.Level)
                {
                    TLog.Error(
                        $"[PureRunAbilityBinder] Missing exact AbilityConfig for {learned.SkillId} Lv{learned.Level}; " +
                        $"falling back to Lv{resolvedLevel}.");
                }

                TryLoad(
                    path,
                    learned.SkillId,
                    learned.Level,
                    resolvedLevel,
                    abilityLoader,
                    configs,
                    loadedPaths,
                    result);
            }

            unit.ApplyAbilityConfigs(configs);
            return result;
        }

        private static void TryLoad(
            string path,
            string skillId,
            int requestedLevel,
            int resolvedLevel,
            Func<string, AbilityConfig> loader,
            ICollection<AbilityConfig> configs,
            ISet<string> loadedPaths,
            PureRunAbilityBindingResult result)
        {
            if (string.IsNullOrWhiteSpace(path) || loadedPaths.Contains(path))
                return;

            AbilityConfig config;
            try
            {
                config = loader(path);
            }
            catch (Exception ex)
            {
                TLog.Error($"[PureRunAbilityBinder] Failed to load '{path}' for {skillId}: {ex.Message}");
                result.MissingSkillIdsInternal.Add(skillId);
                return;
            }

            if (config == null)
            {
                TLog.Error($"[PureRunAbilityBinder] Ability loader returned null for '{path}' ({skillId}).");
                result.MissingSkillIdsInternal.Add(skillId);
                return;
            }

            loadedPaths.Add(path);
            configs.Add(config);
            result.LoadedPathsInternal.Add(path);
            result.ResolvedSkillLevelsInternal[skillId] = resolvedLevel;

            if (resolvedLevel > requestedLevel)
                TLog.Warning($"[PureRunAbilityBinder] Resolved {skillId} above requested level {requestedLevel}.");
        }
    }
}
