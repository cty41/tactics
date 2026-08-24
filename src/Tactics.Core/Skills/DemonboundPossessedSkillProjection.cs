using Tactics.Core.Content;

namespace Tactics.Core.Skills;

/// <summary>
/// Projects a possessed unit's learned skills to their highest published branch level for the
/// current battle only. The projection never mutates <c>LearnedSkills</c>, never unlocks unlearned
/// or master skills, and is recomputed on demand so saves, growth trees and later battles stay intact.
/// </summary>
public static class DemonboundPossessedSkillProjection
{
    public const string ContractId = "DEMONBOUND-POSSESSED-SKILL-PROJECTION-001";

    /// <summary>
    /// Returns the projected skill list when <paramref name="isPossessed"/> is true.
    /// Meditation stays at its fixed level; every other learned skill is swapped for the
    /// highest-level definition of its branch found in <paramref name="catalog"/>.
    /// </summary>
    public static IReadOnlyList<SkillDefinition> Project(
        IReadOnlyList<SkillDefinition> learnedSkills,
        IReadOnlyDictionary<ContentId, SkillDefinition> catalog,
        bool isPossessed)
    {
        ArgumentNullException.ThrowIfNull(learnedSkills);
        ArgumentNullException.ThrowIfNull(catalog);
        if (!isPossessed || learnedSkills.Count == 0) return learnedSkills;

        Dictionary<string, SkillDefinition> highestByBranch = catalog.Values
            .GroupBy(skill => skill.BranchId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key,
                group => group.OrderByDescending(skill => skill.Level)
                    .ThenBy(skill => skill.ContentId.Value, StringComparer.Ordinal).First(),
                StringComparer.Ordinal);

        return learnedSkills.Select(skill =>
            skill.ExecutionKind == SkillExecutionKind.Meditation ||
            !highestByBranch.TryGetValue(skill.BranchId, out SkillDefinition? highest) || highest.Level <= skill.Level
                ? skill
                : highest).ToArray();
    }
}