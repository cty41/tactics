#if TOOLS
using System.Text.Json;
using Tactics.Application.Content;
using Tactics.Application.Skills;
using Tactics.Core.Content;
using Tactics.Core.Skills;

namespace Tactics.Godot.Adapter.Editor;

internal sealed class StartingSkillMigrationDraft
{
    public int SchemaVersion { get; init; }
    public string BatchId { get; init; } = string.Empty;
    public string Classification { get; init; } = string.Empty;
    public StartingSkillDraftSource Source { get; init; } = new();
    public StartingSkillDraftDefinition[] Definitions { get; init; } = Array.Empty<StartingSkillDraftDefinition>();
    public string[] ExternalContentDependencies { get; init; } = Array.Empty<string>();
    public StartingSkillPayloadBoundary PayloadBoundary { get; init; } = new();

    public IReadOnlyDictionary<ContentId, SkillDefinition> Compile()
    {
        SkillDefinitionCompileResult skills = new SkillDefinitionCompiler().Compile(Definitions.Select(item => item.ToApplicationDraft()));
        if (!skills.Succeeded || skills.Definitions is null) throw new InvalidOperationException("Starting-skill draft failed typed Application compilation: " + string.Join("; ", skills.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
        ContentDraft[] ownedSkills = skills.ContentDrafts
            .Where(item => !Definitions.Single(definition => definition.ContentId == item.ContentId.Value).ExternalDependency)
            .ToArray();
        ContentDraft[] externalStatuses = ownedSkills.SelectMany(item => item.References)
            .Distinct()
            .OrderBy(item => item.Value, StringComparer.Ordinal)
            .Select(item => new ContentDraft(item, "buff", 1))
            .ToArray();
        ContentCompileResult content = new ContentCompiler().Compile(ownedSkills.Concat(externalStatuses));
        if (!content.Succeeded)
            throw new InvalidOperationException("Starting-skill unified content compilation failed: " +
                string.Join("; ", content.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
        return skills.Definitions;
    }

    public static StartingSkillMigrationDraft Load(string path)
    {
        StartingSkillMigrationDraft? draft = JsonSerializer.Deserialize<StartingSkillMigrationDraft>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (draft is null || draft.SchemaVersion != 1 || draft.BatchId != "pure-run-starting-skills-v1" || draft.Definitions.Length != 12 || !draft.ExternalContentDependencies.SequenceEqual(new[] { "skill.poison-spear.lv1" }) || draft.PayloadBoundary.ThirdPartyPayloadCopied || draft.PayloadBoundary.VisualAcceptance != "not_applicable_gameplay_only_no_visual_payload")
            throw new InvalidOperationException("Starting-skill typed draft identity is invalid.");
        return draft;
    }
}

internal sealed class StartingSkillDraftSource { public string SourceTag { get; init; } = string.Empty; public string SourceCommit { get; init; } = string.Empty; public string UnityVersion { get; init; } = string.Empty; public string ExporterVersion { get; init; } = string.Empty; public string ExportHash { get; init; } = string.Empty; }
internal sealed class StartingSkillPayloadBoundary { public string Presentation { get; init; } = string.Empty; public bool ThirdPartyPayloadCopied { get; init; } public string VisualAcceptance { get; init; } = string.Empty; public string ManualGameplayAcceptance { get; init; } = string.Empty; }
internal sealed class StartingSkillSourceAudit { public bool PresentationPayloadCopied { get; init; } public bool ThirdPartyPayloadCopied { get; init; } }
internal sealed class StartingSkillDraftDefinition
{
    public string ContentId { get; init; } = string.Empty; public string SourceId { get; init; } = string.Empty; public string DisplayName { get; init; } = string.Empty; public string Description { get; init; } = string.Empty;
    public string BranchId { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty; public string Kind { get; init; } = string.Empty; public int Level { get; init; } public int ManaCost { get; init; } public int MinRange { get; init; } public int MaxRange { get; init; }
    public string ExecutionKind { get; init; } = string.Empty; public int Damage { get; init; } public string DamageKind { get; init; } = string.Empty; public string StatusContentId { get; init; } = string.Empty; public int StatusDuration { get; init; }
    public bool Hidden { get; init; } public bool ExternalDependency { get; init; } public string SourcePath { get; init; } = string.Empty; public string SourceGuid { get; init; } = string.Empty; public long SourceLocalFileId { get; init; }
    public bool IsBasicAbility { get; init; } public int MaxUsesPerTurn { get; init; }
    public string RequiredAttribute { get; init; } = string.Empty; public int MinimumAttribute { get; init; }
    public bool GrowthVisible { get; init; }
    public string GraphPath { get; init; } = string.Empty; public string GraphDependencyHash { get; init; } = string.Empty; public StartingSkillSourceAudit SourceAudit { get; init; } = new();
    public SkillDefinitionDraft ToApplicationDraft() => new() { ContentId = ContentId, SourceId = SourceId, Role = Role, Kind = Kind, Level = Level, ManaCost = ManaCost, MinRange = MinRange, MaxRange = MaxRange, ExecutionKind = ExecutionKind, Damage = Damage, DamageKind = DamageKind, StatusContentId = StatusContentId, StatusDuration = StatusDuration, Hidden = Hidden, ExternalDependency = ExternalDependency, IsBasicAbility = IsBasicAbility, MaxUsesPerTurn = MaxUsesPerTurn, BranchId = BranchId, RequiredAttribute = RequiredAttribute, MinimumAttribute = MinimumAttribute, GrowthVisible = GrowthVisible, EffectScaling = EffectScalingFor(ExecutionKind), AccuracyFactor = 1m };

    private static string EffectScalingFor(string executionKind) => executionKind switch
    {
        nameof(SkillExecutionKind.MeleeAttack) or nameof(SkillExecutionKind.Thrust) or
            nameof(SkillExecutionKind.MultiStab) => nameof(SkillEffectScalingKind.MeleePhysical),
        nameof(SkillExecutionKind.RangedAttack) or nameof(SkillExecutionKind.HeavyShot) or
            nameof(SkillExecutionKind.PoisonSpear) => nameof(SkillEffectScalingKind.RangedPhysical),
        nameof(SkillExecutionKind.MagicAttack) or nameof(SkillExecutionKind.Fireball) or
            nameof(SkillExecutionKind.IceBolt) or nameof(SkillExecutionKind.Lightning) or
            nameof(SkillExecutionKind.BoneSpear) => nameof(SkillEffectScalingKind.Magical),
        nameof(SkillExecutionKind.RecoverSpear) => nameof(SkillEffectScalingKind.Healing),
        nameof(SkillExecutionKind.IceArmor) or nameof(SkillExecutionKind.BoneShield) =>
            nameof(SkillEffectScalingKind.Shield),
        _ => nameof(SkillEffectScalingKind.None)
    };
}
#endif
