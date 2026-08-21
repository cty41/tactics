#if TOOLS
using Tactics.Application.Authoring;
using Tactics.Core.Skills;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

public static class SkillAuthoringEditorService
{
    public static SkillAuthoringDocument Read(SkillDefinitionResource resource) => new(resource.ToCoreDefinition(),
        resource.DisplayName, resource.Description, resource.SourcePath, resource.SourceGuid, resource.SourceLocalFileId,
        resource.GraphPath, resource.GraphDependencyHash, Enum.Parse<SkillAuthoringSourceKind>(resource.AuthoringSourceKindValue));

    public static void Write(SkillDefinitionResource resource, SkillAuthoringDocument document)
    {
        if (resource.ContentIdValue != document.ContentId) throw new InvalidOperationException("Skill document identity differs from the Resource identity.");
        SkillDefinition d = document.Definition; SkillExecutionProfile p = d.ExecutionProfile;
        resource.SourceId = d.SourceId; resource.DisplayName = document.DisplayName; resource.Description = document.Description;
        resource.RoleValue = d.Role.ToString(); resource.KindValue = d.Kind.ToString(); resource.Level = d.Level; resource.ManaCost = d.ManaCost;
        resource.MinRange = d.MinRange; resource.MaxRange = d.MaxRange; resource.ExecutionKindValue = d.ExecutionKind.ToString(); resource.Damage = d.Damage;
        resource.DamageKindValue = d.DamageKind.ToString(); resource.StatusContentIdValue = d.StatusContentId?.Value ?? string.Empty; resource.StatusDuration = d.StatusDuration;
        resource.Hidden = d.Hidden; resource.ExternalDependency = d.ExternalDependency; resource.IsBasicAbility = d.IsBasicAbility; resource.MaxUsesPerTurn = d.MaxUsesPerTurn; resource.CanCrit = d.CanCrit;
        resource.BranchId = d.BranchId; resource.PrerequisiteContentIdValue = d.PrerequisiteContentId?.Value ?? string.Empty; resource.PrerequisiteBranchId = d.PrerequisiteBranchId; resource.GrowthVisible = d.GrowthVisible; resource.RequiredAttribute = d.RequiredAttribute; resource.MinimumAttribute = d.MinimumAttribute;
        resource.AreaRadius = p.AreaRadius; resource.OrderedTargetCount = p.OrderedTargetCount; resource.SummonDefinitionIdValue = p.SummonDefinitionId?.Value ?? string.Empty; resource.SummonCount = p.SummonCount; resource.SummonLimit = p.SummonLimit; resource.SummonCategory = p.SummonCategory; resource.RequiresCorpse = p.RequiresCorpse; resource.IgnoreLineOfSight = p.IgnoreLineOfSight; resource.ShieldMultiplier = p.ShieldMultiplier; resource.ShieldAbsorbsAllDamage = p.ShieldAbsorbsAllDamage; resource.CleanseHarmful = p.CleanseHarmful; resource.SecondaryDamage = p.SecondaryDamage; resource.AreaShape = p.AreaShape; resource.StatusChancePercent = p.StatusChancePercent; resource.DetonateStatusContentIdValue = p.DetonateStatusContentId?.Value ?? string.Empty; resource.BounceRange = p.BounceRange; resource.BounceCount = p.BounceCount; resource.PierceAll = p.PierceAll; resource.AllowsEmptyTarget = p.AllowsEmptyTarget; resource.MovementDamagePerCell = p.MovementDamagePerCell; resource.SummonAttackContentIdValue = p.SummonAttackContentId?.Value ?? string.Empty; resource.CorruptionCost = p.CorruptionCost; resource.DamageScalingValue = p.DamageScaling.ToString(); resource.LifeStealPercent = p.LifeStealPercent;
        resource.SourcePath = document.SourcePath; resource.SourceGuid = document.SourceGuid; resource.SourceLocalFileId = document.SourceLocalFileId; resource.GraphPath = document.GraphPath; resource.GraphDependencyHash = document.GraphDependencyHash; resource.AuthoringSourceKindValue = document.SourceKind.ToString();
        _ = resource.ToCoreDefinition();
    }
}
#endif
