using System.Text.Json;
using NUnit.Framework;
using Tactics.Application.Content;
using Tactics.Application.Items;
using Tactics.Application.Statuses;
using Tactics.Core.Content;

namespace Tactics.Application.Tests;

public sealed class StatusItemDefinitionCompilerTests
{
    [Test]
    public void FrozenGolden_CompilesFourteenStatusesAndFifteenItemsThroughUnifiedContent()
    {
        (StatusDefinitionDraft[] statuses, ConsumableDefinitionDraft[] consumables,
            EquipmentDefinitionDraft[] equipment) = LoadGolden();

        StatusDefinitionCompileResult statusResult = new StatusDefinitionCompiler().Compile(statuses);
        ItemDefinitionCompileResult itemResult = new ItemDefinitionCompiler().Compile(consumables, equipment);
        ContentCompileResult content = new ContentCompiler().Compile(
            statusResult.ContentDrafts.Concat(itemResult.ContentDrafts));

        Assert.Multiple(() =>
        {
            Assert.That(statusResult.Succeeded, Is.True, string.Join(Environment.NewLine, statusResult.Diagnostics));
            Assert.That(statusResult.Definitions, Has.Count.EqualTo(14));
            Assert.That(itemResult.Succeeded, Is.True, string.Join(Environment.NewLine, itemResult.Diagnostics));
            Assert.That(itemResult.Consumables, Has.Count.EqualTo(3));
            Assert.That(itemResult.Equipment, Has.Count.EqualTo(12));
            Assert.That(content.Succeeded, Is.True, string.Join(Environment.NewLine, content.Diagnostics));
            Assert.That(content.Snapshot!.Entries, Has.Count.EqualTo(29));
            Assert.That(statusResult.ContentDrafts.Single(
                item => item.ContentId == new ContentId("buff.poison")).Properties["externalDependency"],
                Is.EqualTo("true"));
        });
    }

    [Test]
    public void Compilers_RejectReferenceEnumAndDerivedItemDrift()
    {
        (StatusDefinitionDraft[] statuses, ConsumableDefinitionDraft[] consumables,
            EquipmentDefinitionDraft[] equipment) = LoadGolden();
        StatusDefinitionCompileResult statusResult = new StatusDefinitionCompiler().Compile(
            statuses.Select(status => status.ContentId == "buff.ice-armor.lv2"
                ? status with { MeleeRetaliationStatusId = "buff.missing" }
                : status));
        ItemDefinitionCompileResult itemResult = new ItemDefinitionCompiler().Compile(
            consumables.Select(item => item.ContentId == "item.consumable.life-potion"
                ? item with { EffectKind = "Unknown" }
                : item),
            equipment.Select(item => item.ContentId == "item.equipment.sword-01"
                ? item with { StrengthBonus = -1 }
                : item));
        ContentCompileResult content = new ContentCompiler().Compile(statusResult.ContentDrafts);

        Assert.Multiple(() =>
        {
            Assert.That(content.Diagnostics.Select(item => item.Code), Does.Contain("content.missing_reference"));
            Assert.That(itemResult.Diagnostics.Select(item => item.Code), Does.Contain("item.unknown_enum"));
            Assert.That(itemResult.Diagnostics.Select(item => item.Code), Does.Contain("item.invalid_parameter"));
        });
    }

    private static (
        StatusDefinitionDraft[] Statuses,
        ConsumableDefinitionDraft[] Consumables,
        EquipmentDefinitionDraft[] Equipment) LoadGolden()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Golden", "buff-item-batch-v1.json")));
        StatusDefinitionDraft[] statuses = document.RootElement.GetProperty("buffs").EnumerateArray()
            .Select(entry =>
            {
                JsonElement definition = entry.GetProperty("definition");
                return new StatusDefinitionDraft
                {
                    ContentId = definition.GetProperty("contentId").GetString()!,
                    SourceId = definition.GetProperty("sourceId").GetString()!,
                    DefaultDuration = definition.GetProperty("defaultDuration").GetInt32(),
                    CanAct = definition.GetProperty("canAct").GetBoolean(),
                    Polarity = definition.GetProperty("polarity").GetString()!,
                    EffectKind = definition.GetProperty("effectType").GetString()!,
                    TriggerTiming = definition.GetProperty("triggerTiming").GetString()!,
                    RefreshStrategy = definition.GetProperty("refreshStrategy").GetString()!,
                    CurseCategory = definition.GetProperty("curseCategory").GetString()!,
                    DamagePerTurn = definition.GetProperty("damagePerTurn").GetSingle(),
                    ElementKind = definition.GetProperty("elementType").GetString()!,
                    DamageCategory = definition.GetProperty("damageCategory").GetString()!,
                    SpeedModifier = definition.GetProperty("speedModifier").GetSingle(),
                    DamageReductionPercent = definition.GetProperty("damageReductionPercent").GetSingle(),
                    MeleeRetaliationStatusId = definition.GetProperty("meleeRetaliationBuffContentId").GetString()!,
                    MeleeRetaliationDuration = definition.GetProperty("meleeRetaliationDuration").GetInt32(),
                    ExternalDependency = definition.GetProperty("externalDependency").GetBoolean()
                };
            }).ToArray();
        ConsumableDefinitionDraft[] consumables = document.RootElement.GetProperty("consumables").EnumerateArray()
            .Select(item => new ConsumableDefinitionDraft
            {
                ContentId = item.GetProperty("contentId").GetString()!,
                SourceId = item.GetProperty("sourceId").GetString()!,
                DisplayName = item.GetProperty("displayName").GetString()!,
                Description = item.GetProperty("description").GetString()!,
                Rarity = item.GetProperty("rarity").GetString()!,
                Price = item.GetProperty("price").GetInt32(),
                MaxCharges = item.GetProperty("maxCharges").GetInt32(),
                EffectKind = item.GetProperty("effectKind").GetString()!,
                Magnitude = item.GetProperty("magnitude").GetSingle(),
                MaxRange = item.GetProperty("maxRange").GetInt32(),
                TargetMode = item.GetProperty("targetMode").GetString()!
            }).ToArray();
        EquipmentDefinitionDraft[] equipment = document.RootElement.GetProperty("equipment").EnumerateArray()
            .Select(item => new EquipmentDefinitionDraft
            {
                ContentId = item.GetProperty("contentId").GetString()!,
                SourceId = item.GetProperty("sourceId").GetString()!,
                DisplayName = item.GetProperty("displayName").GetString()!,
                Slot = item.GetProperty("slot").GetString()!,
                Rarity = item.GetProperty("rarity").GetString()!,
                Price = item.GetProperty("price").GetInt32(),
                StrengthBonus = item.GetProperty("strengthBonus").GetInt32(),
                AgilityBonus = item.GetProperty("agilityBonus").GetInt32(),
                ConstitutionBonus = item.GetProperty("constitutionBonus").GetInt32(),
                IntelligenceBonus = item.GetProperty("intelligenceBonus").GetInt32(),
                CharismaBonus = item.GetProperty("charismaBonus").GetInt32(),
                LuckBonus = item.GetProperty("luckBonus").GetInt32()
            }).ToArray();
        return (statuses, consumables, equipment);
    }
}
