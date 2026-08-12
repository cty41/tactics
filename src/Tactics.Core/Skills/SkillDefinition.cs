using Tactics.Core.Content;
using Tactics.Core.Statuses;

namespace Tactics.Core.Skills;

public enum SkillRole { Any, Mage, Necromancer, Amazon }
public enum SkillKind { Basic, Active, Passive, Utility }
public enum SkillDamageKind { None, Physical, Magical }
public enum SkillExecutionKind
{
    MagicAttack,
    MeleeAttack,
    Fireball,
    IceBolt,
    Lightning,
    SummonSkeleton,
    AmplifyDamage,
    BoneSpear,
    Thrust,
    PoisonSpear,
    CombatTechniques,
    PickupSpear,
    RangedAttack,
    ChargeStrike,
    HeavyShot,
    AreaBlast,
    SummonFireDemon,
    IceArmor,
    Teleport,
    SummonSkeletonMage,
    FearCurse,
    BoneShield,
    MultiStab,
    RecoverSpear,
    Decoy
}

/// <summary>Optional normalized parameters used by the complete Pure Run Lv1/Lv2 skill set.</summary>
public sealed record SkillExecutionProfile(
    int AreaRadius = 0,
    int OrderedTargetCount = 0,
    ContentId? SummonDefinitionId = null,
    int SummonCount = 0,
    int SummonLimit = 0,
    string SummonCategory = "",
    bool RequiresCorpse = false,
    bool IgnoreLineOfSight = false,
    int ShieldMultiplier = 0,
    bool ShieldAbsorbsAllDamage = false,
    bool CleanseHarmful = false,
    int SecondaryDamage = 0);

/// <summary>Normalized engine-neutral execution contract for one migrated skill level.</summary>
public sealed record SkillDefinition
{
    public SkillDefinition(
        ContentId contentId,
        string sourceId,
        SkillRole role,
        SkillKind kind,
        int level,
        int manaCost,
        int minRange,
        int maxRange,
        SkillExecutionKind executionKind,
        int damage,
        SkillDamageKind damageKind,
        ContentId? statusContentId = null,
        int statusDuration = 0,
        bool hidden = false,
        bool externalDependency = false,
        bool? isBasicAbility = null,
        int maxUsesPerTurn = 0,
        string branchId = "",
        ContentId? prerequisiteContentId = null,
        bool growthVisible = true,
        SkillExecutionProfile? executionProfile = null)
    {
        if (string.IsNullOrWhiteSpace(sourceId)) throw new ArgumentException("SourceId cannot be empty.", nameof(sourceId));
        if (!Enum.IsDefined(role) || !Enum.IsDefined(kind) || !Enum.IsDefined(executionKind) || !Enum.IsDefined(damageKind)) throw new ArgumentOutOfRangeException(nameof(executionKind));
        if (level <= 0 || manaCost < 0 || minRange < 0 || maxRange < minRange || damage < 0 || maxUsesPerTurn < 0) throw new ArgumentOutOfRangeException(nameof(level));
        if ((statusContentId is null) != (statusDuration == 0)) throw new ArgumentException("Status identity and duration must be configured together.");
        ContentId = contentId;
        SourceId = sourceId.Trim();
        Role = role;
        Kind = kind;
        Level = level;
        ManaCost = manaCost;
        MinRange = minRange;
        MaxRange = maxRange;
        ExecutionKind = executionKind;
        Damage = damage;
        DamageKind = damageKind;
        StatusContentId = statusContentId;
        StatusDuration = statusDuration;
        Hidden = hidden;
        ExternalDependency = externalDependency;
        IsBasicAbility = isBasicAbility ?? kind == SkillKind.Basic;
        MaxUsesPerTurn = maxUsesPerTurn;
        BranchId = string.IsNullOrWhiteSpace(branchId) ? contentId.Value : branchId.Trim();
        PrerequisiteContentId = prerequisiteContentId;
        GrowthVisible = growthVisible;
        ExecutionProfile = executionProfile ?? new SkillExecutionProfile();
    }

    public ContentId ContentId { get; }
    public string SourceId { get; }
    public SkillRole Role { get; }
    public SkillKind Kind { get; }
    public int Level { get; }
    public int ManaCost { get; }
    public int MinRange { get; }
    public int MaxRange { get; }
    public SkillExecutionKind ExecutionKind { get; }
    public int Damage { get; }
    public SkillDamageKind DamageKind { get; }
    public ContentId? StatusContentId { get; }
    public int StatusDuration { get; }
    public bool Hidden { get; }
    public bool ExternalDependency { get; }
    public bool IsBasicAbility { get; }
    public int MaxUsesPerTurn { get; }
    public string BranchId { get; }
    public ContentId? PrerequisiteContentId { get; }
    public bool GrowthVisible { get; }
    public SkillExecutionProfile ExecutionProfile { get; }
    public bool IsPassive => Kind == SkillKind.Passive;
    public int AreaRadius => ExecutionProfile.AreaRadius > 0 ? ExecutionProfile.AreaRadius : ExecutionKind == SkillExecutionKind.AreaBlast ? 2 : 0;
    public bool UsesLineTargeting => ExecutionKind is SkillExecutionKind.Fireball or SkillExecutionKind.IceBolt or SkillExecutionKind.BoneSpear or SkillExecutionKind.Thrust;
    public bool RequiresLineOfSight => !ExecutionProfile.IgnoreLineOfSight &&
        ExecutionKind is (SkillExecutionKind.MagicAttack or SkillExecutionKind.Fireball or SkillExecutionKind.IceBolt or SkillExecutionKind.RangedAttack or SkillExecutionKind.HeavyShot or SkillExecutionKind.ChargeStrike);
}

public sealed class SkillCatalogDefinition
{
    private readonly IReadOnlyDictionary<ContentId, SkillDefinition> _definitions;

    public SkillCatalogDefinition(IEnumerable<SkillDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        SkillDefinition[] values = definitions.OrderBy(item => item.ContentId.Value, StringComparer.Ordinal).ToArray();
        if (values.Length == 0 || values.Select(item => item.ContentId).Distinct().Count() != values.Length)
            throw new ArgumentException("Skill Catalog must contain unique definitions.", nameof(definitions));
        _definitions = values.ToDictionary(item => item.ContentId);
    }

    public IReadOnlyDictionary<ContentId, SkillDefinition> Definitions => _definitions;
    public SkillDefinition Get(ContentId contentId) => _definitions.TryGetValue(contentId, out SkillDefinition? value) ? value : throw new KeyNotFoundException(contentId.Value);
}
