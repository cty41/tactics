#if TOOLS
using System.Text.Json;
using Tactics.Application.Content;
using Tactics.Application.Items;
using Tactics.Application.Statuses;
using Tactics.Core.Content;
using Tactics.Core.Items;
using Tactics.Core.Statuses;

namespace Tactics.Godot.Adapter.Editor;

internal sealed record CompiledBuffItemDefinitions(
    IReadOnlyDictionary<ContentId, StatusDefinition> Statuses,
    IReadOnlyDictionary<ContentId, ConsumableDefinition> Consumables,
    IReadOnlyDictionary<ContentId, EquipmentDefinition> Equipment,
    ContentSnapshot Snapshot);

internal sealed class BuffItemMigrationDraft
{
    public int SchemaVersion { get; init; }
    public string BatchId { get; init; } = string.Empty;
    public string Classification { get; init; } = string.Empty;
    public BuffItemDraftSource Source { get; init; } = new();
    public BuffItemPayloadBoundary PayloadBoundary { get; init; } = new();
    public string[] ExternalContentDependencies { get; init; } = Array.Empty<string>();
    public BuffItemDraftStatus[] Buffs { get; init; } = Array.Empty<BuffItemDraftStatus>();
    public BuffItemDraftConsumable[] Consumables { get; init; } = Array.Empty<BuffItemDraftConsumable>();
    public BuffItemDraftEquipment[] Equipment { get; init; } = Array.Empty<BuffItemDraftEquipment>();
    public BuffItemDraftConsumablePool[] ConsumablePools { get; init; } = Array.Empty<BuffItemDraftConsumablePool>();

    public CompiledBuffItemDefinitions CompileApplicationDefinitions()
    {
        StatusDefinitionCompileResult statuses = new StatusDefinitionCompiler().Compile(
            Buffs.Select(buff => buff.ToApplicationDraft()));
        ItemDefinitionCompileResult items = new ItemDefinitionCompiler().Compile(
            Consumables.Select(item => item.ToApplicationDraft()),
            Equipment.Select(item => item.ToApplicationDraft()));
        ContentCompileResult content = new ContentCompiler().Compile(
            statuses.ContentDrafts.Concat(items.ContentDrafts));
        if (!statuses.Succeeded || statuses.Definitions is null || !items.Succeeded ||
            items.Consumables is null || items.Equipment is null || !content.Succeeded || content.Snapshot is null)
        {
            IEnumerable<ContentDiagnostic> diagnostics = statuses.Diagnostics
                .Concat(items.Diagnostics)
                .Concat(content.Diagnostics);
            throw new InvalidOperationException(
                "Buff/Item typed draft failed Application compilation: " +
                string.Join("; ", diagnostics.Select(item => $"{item.Code}: {item.Message}")));
        }
        return new CompiledBuffItemDefinitions(
            statuses.Definitions,
            items.Consumables,
            items.Equipment,
            content.Snapshot);
    }

    public static BuffItemMigrationDraft Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Pure Run Buff/Item typed migration draft is missing.", path);
        BuffItemMigrationDraft? draft = JsonSerializer.Deserialize<BuffItemMigrationDraft>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (draft is null || draft.SchemaVersion != 1 || draft.BatchId != "pure-run-buffs-items-v1" ||
            draft.Classification != "disposable_typed_buff_item_migration_draft" ||
            draft.Buffs.Length != 14 || draft.Consumables.Length != 3 || draft.Equipment.Length != 12 ||
            draft.ConsumablePools.Length != 1 ||
            !draft.ExternalContentDependencies.SequenceEqual(new[] { "buff.poison" }, StringComparer.Ordinal) ||
            draft.PayloadBoundary.BuffIcons != "audit_only_not_copied" ||
            draft.PayloadBoundary.IconPayloadCopied || draft.PayloadBoundary.ThirdPartyPayloadCopied ||
            draft.PayloadBoundary.VisualAcceptance != "not_applicable_no_visual_payload")
        {
            throw new InvalidOperationException("Pure Run Buff/Item typed draft identity is invalid.");
        }
        if (draft.Buffs.Count(buff => buff.ExternalDependency) != 1 ||
            draft.Buffs.Single(buff => buff.ExternalDependency).ContentId != "buff.poison")
        {
            throw new InvalidOperationException("Pure Run Buff/Item external ownership boundary is invalid.");
        }
        return draft;
    }
}

internal sealed class BuffItemDraftSource
{
    public string SourceTag { get; init; } = string.Empty;
    public string SourceCommit { get; init; } = string.Empty;
    public string UnityVersion { get; init; } = string.Empty;
    public string ExporterVersion { get; init; } = string.Empty;
    public string ExportHash { get; init; } = string.Empty;
    public BuffItemJsonSource ConsumablesJson { get; init; } = new();
    public BuffItemJsonSource EquipmentJson { get; init; } = new();
}

internal sealed class BuffItemJsonSource
{
    public string SourcePath { get; init; } = string.Empty;
    public string GitBlobSha1 { get; init; } = string.Empty;
    public string Sha256 { get; init; } = string.Empty;
    public string MeasuredSha256 { get; init; } = string.Empty;
}

internal sealed class BuffItemPayloadBoundary
{
    public string BuffIcons { get; init; } = string.Empty;
    public bool IconPayloadCopied { get; init; }
    public bool ThirdPartyPayloadCopied { get; init; }
    public string VisualAcceptance { get; init; } = string.Empty;
}

internal sealed class BuffItemIconAudit
{
    public string SourcePath { get; init; } = string.Empty;
    public string SourceGuid { get; init; } = string.Empty;
    public long SourceLocalFileId { get; init; }
    public string DependencyHash { get; init; } = string.Empty;
    public bool PayloadCopied { get; init; }
}

internal sealed class BuffItemDraftStatus
{
    public string ContentId { get; init; } = string.Empty;
    public string SourceId { get; init; } = string.Empty;
    public string SourcePath { get; init; } = string.Empty;
    public string SourceGuid { get; init; } = string.Empty;
    public long SourceLocalFileId { get; init; }
    public int DefaultDuration { get; init; }
    public bool CanAct { get; init; }
    public string Polarity { get; init; } = string.Empty;
    public string EffectType { get; init; } = string.Empty;
    public string TriggerTiming { get; init; } = string.Empty;
    public string CurseCategory { get; init; } = string.Empty;
    public float DamagePerTurn { get; init; }
    public string ElementType { get; init; } = string.Empty;
    public string DamageCategory { get; init; } = string.Empty;
    public string RefreshStrategy { get; init; } = string.Empty;
    public float SpeedModifier { get; init; }
    public float DamageReductionPercent { get; init; }
    public string MeleeRetaliationBuffContentId { get; init; } = string.Empty;
    public int MeleeRetaliationDuration { get; init; }
    public BuffItemIconAudit IconAudit { get; init; } = new();
    public bool ExternalDependency { get; init; }

    public StatusDefinitionDraft ToApplicationDraft() => new()
    {
        ContentId = ContentId,
        SourceId = SourceId,
        DefaultDuration = DefaultDuration,
        CanAct = CanAct,
        Polarity = Polarity,
        EffectKind = EffectType,
        TriggerTiming = TriggerTiming,
        RefreshStrategy = RefreshStrategy,
        CurseCategory = CurseCategory,
        DamagePerTurn = DamagePerTurn,
        ElementKind = ElementType,
        DamageCategory = DamageCategory,
        SpeedModifier = SpeedModifier,
        DamageReductionPercent = DamageReductionPercent,
        MeleeRetaliationStatusId = MeleeRetaliationBuffContentId,
        MeleeRetaliationDuration = MeleeRetaliationDuration,
        ExternalDependency = ExternalDependency
    };
}

internal sealed class BuffItemDraftConsumable
{
    public string ContentId { get; init; } = string.Empty;
    public string SourceId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Rarity { get; init; } = string.Empty;
    public int Price { get; init; }
    public int MaxCharges { get; init; }
    public string EffectKind { get; init; } = string.Empty;
    public float Magnitude { get; init; }
    public int MaxRange { get; init; }
    public string TargetMode { get; init; } = string.Empty;

    public ConsumableDefinitionDraft ToApplicationDraft() => new()
    {
        ContentId = ContentId,
        SourceId = SourceId,
        DisplayName = DisplayName,
        Description = Description,
        Rarity = Rarity,
        Price = Price,
        MaxCharges = MaxCharges,
        EffectKind = EffectKind,
        Magnitude = Magnitude,
        MaxRange = MaxRange,
        TargetMode = TargetMode
    };
}

internal sealed class BuffItemDraftEquipment
{
    public string ContentId { get; init; } = string.Empty;
    public string SourceId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Slot { get; init; } = string.Empty;
    public string Rarity { get; init; } = string.Empty;
    public int Price { get; init; }
    public int StrengthBonus { get; init; }
    public int AgilityBonus { get; init; }
    public int ConstitutionBonus { get; init; }
    public int IntelligenceBonus { get; init; }
    public int CharismaBonus { get; init; }
    public int LuckBonus { get; init; }

    public EquipmentDefinitionDraft ToApplicationDraft() => new()
    {
        ContentId = ContentId,
        SourceId = SourceId,
        DisplayName = DisplayName,
        Slot = Slot,
        Rarity = Rarity,
        Price = Price,
        StrengthBonus = StrengthBonus,
        AgilityBonus = AgilityBonus,
        ConstitutionBonus = ConstitutionBonus,
        IntelligenceBonus = IntelligenceBonus,
        CharismaBonus = CharismaBonus,
        LuckBonus = LuckBonus
    };
}

internal sealed class BuffItemDraftConsumablePool
{
    public string SourceId { get; init; } = string.Empty;
    public BuffItemDraftPoolEntry[] Entries { get; init; } = Array.Empty<BuffItemDraftPoolEntry>();
}

internal sealed class BuffItemDraftPoolEntry
{
    public string ConsumableContentId { get; init; } = string.Empty;
    public float Weight { get; init; }
}
#endif
