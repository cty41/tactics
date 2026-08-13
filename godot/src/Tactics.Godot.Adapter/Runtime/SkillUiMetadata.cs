using Tactics.Core.Content;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Adapter-owned display metadata kept outside deterministic gameplay definitions.</summary>
public sealed record SkillUiMetadata(
    ContentId ContentId,
    string DisplayName,
    string Description,
    int Level,
    int ManaCost,
    int MinRange,
    int MaxRange,
    bool IsPassive,
    string RequiredAttribute,
    int MinimumAttribute,
    string PrerequisiteBranchId)
{
    public static SkillUiMetadata From(SkillDefinitionResource resource) => new(
        new ContentId(resource.ContentIdValue), WithoutLevelSuffix(resource.DisplayName, resource.Level),
        resource.Description, resource.Level, resource.ManaCost, resource.MinRange, resource.MaxRange,
        string.Equals(resource.KindValue, "Passive", StringComparison.Ordinal), resource.RequiredAttribute,
        resource.MinimumAttribute, resource.PrerequisiteBranchId);

    public static SkillUiMetadata From(PoisonSpearSkillResource resource) => new(
        resource.ContentId, WithoutLevelSuffix(resource.DisplayName, 1), resource.Description, 1,
        resource.ManaCost, 1, resource.Range, false, "Agility", 5, string.Empty);

    private static string WithoutLevelSuffix(string value, int level)
    {
        string suffix = $" Lv{level}";
        return value.EndsWith(suffix, StringComparison.Ordinal) ? value[..^suffix.Length] : value;
    }
}
