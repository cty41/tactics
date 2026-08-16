using Tactics.Core.Content;
using Tactics.Core.Runs;

namespace Tactics.Application.Runs;

/// <summary>Validates cross-catalog references required before a playable run can start.</summary>
public static class PureRunContentValidator
{
    public static void ValidateSkillReferences(
        PureRunDefinition definition,
        IEnumerable<ContentId> availableSkillIds)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(availableSkillIds);
        HashSet<ContentId> available = availableSkillIds.ToHashSet();
        ContentId[] missing = definition.Party
            .SelectMany(template => template.EffectiveStartingSkillChoices.Append(template.StartingSkillContentId))
            .Distinct()
            .Where(id => !available.Contains(id))
            .OrderBy(id => id.Value, StringComparer.Ordinal)
            .ToArray();
        if (missing.Length > 0)
            throw new ArgumentException(
                $"Pure Run references skills missing from the canonical catalog: {string.Join(", ", missing)}.",
                nameof(availableSkillIds));
    }
}
