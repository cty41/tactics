using GdUnit4;
using Godot;
using Tactics.Godot.Adapter.Runtime;
using static GdUnit4.Assertions;

namespace Tactics.Godot.Tests;

[TestSuite]
public class BuffItemBatchGodotTests
{
    private const string BatchCatalogPath = "res://content/buffs_items/ContentCatalog.tres";
    private const string GlobalCatalogPath = "res://content/ContentCatalog.tres";

    [TestCase]
    [RequireGodotRuntime]
    public void BatchAndCanonicalCatalogsValidateWithExternalPoisonOwnership()
    {
        var batchCatalog = ResourceLoader.Load<GodotResourceCatalog>(BatchCatalogPath);
        var globalCatalog = ResourceLoader.Load<GodotResourceCatalog>(GlobalCatalogPath);
        AssertThat(batchCatalog).IsNotNull();
        AssertThat(globalCatalog).IsNotNull();
        if (batchCatalog is null || globalCatalog is null)
            return;

        BuffItemBatchValidation validation = BuffItemBatchValidator.Validate(batchCatalog, globalCatalog);
        AssertThat(validation.BatchCatalogEntryCount).IsEqual(29);
        AssertThat(validation.GlobalCatalogEntryCount is 114 or 115 or 116 or 119 or 123 or 124 or 125).IsTrue();
        AssertThat(validation.StatusCount).IsEqual(14);
        AssertThat(validation.ConsumableCount).IsEqual(3);
        AssertThat(validation.EquipmentCount).IsEqual(12);

        GodotResourceEntry poison = batchCatalog.Entries.Single(entry =>
            entry.ContentIdValue == "buff.poison");
        AssertThat(poison.DiagnosticPathValue)
            .IsEqual("res://content/poison_spear/PoisonBuff.tres");
        AssertThat(batchCatalog.Entries.Count(entry =>
            entry.DiagnosticPathValue.StartsWith("res://content/poison_spear/", StringComparison.Ordinal)))
            .IsEqual(1);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GeneratedStatusesRetainAuditOnlyIconsAndTypedReferences()
    {
        var catalog = ResourceLoader.Load<GodotResourceCatalog>(BatchCatalogPath);
        AssertThat(catalog).IsNotNull();
        if (catalog is null)
            return;

        StatusDefinitionResource[] generatedStatuses = catalog.Entries
            .Where(entry => entry.ResourceTypeIdValue == "buff" && entry.ContentIdValue != "buff.poison")
            .Select(entry => ResourceLoader.Load<StatusDefinitionResource>(entry.DiagnosticPathValue))
            .Where(resource => resource is not null)
            .Cast<StatusDefinitionResource>()
            .ToArray();
        AssertThat(generatedStatuses.Length).IsEqual(13);
        AssertThat(generatedStatuses.All(status => !status.IconPayloadCopied)).IsTrue();
        AssertThat(generatedStatuses.Count(status => !string.IsNullOrEmpty(status.IconSourcePath)))
            .IsEqual(3);
        AssertThat(generatedStatuses.Where(status => !string.IsNullOrEmpty(status.IconSourcePath)).All(status =>
            !string.IsNullOrEmpty(status.IconSourceGuid) && status.IconSourceLocalFileId > 0 &&
            !string.IsNullOrEmpty(status.IconDependencyHash))).IsTrue();

        StatusDefinitionResource iceArmor = generatedStatuses.Single(status =>
            status.ContentIdValue == "buff.ice-armor.lv2");
        AssertThat(iceArmor.MeleeRetaliationStatusIdValue).IsEqual("buff.slow");
        AssertThat(iceArmor.ToCoreDefinition().MeleeRetaliationStatusId?.Value).IsEqual("buff.slow");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GeneratedItemsLoadAsTypedResourcesAndCompileToCore()
    {
        var catalog = ResourceLoader.Load<GodotResourceCatalog>(BatchCatalogPath);
        AssertThat(catalog).IsNotNull();
        if (catalog is null)
            return;

        Resource[] items = catalog.Entries
            .Where(entry => entry.ResourceTypeIdValue == "item")
            .Select(entry => ResourceLoader.Load(entry.DiagnosticPathValue))
            .Where(resource => resource is not null)
            .Cast<Resource>()
            .ToArray();
        ConsumableDefinitionResource[] consumables = items.OfType<ConsumableDefinitionResource>().ToArray();
        EquipmentDefinitionResource[] equipment = items.OfType<EquipmentDefinitionResource>().ToArray();
        AssertThat(items.Length).IsEqual(15);
        AssertThat(consumables.Length).IsEqual(3);
        AssertThat(equipment.Length).IsEqual(12);
        AssertThat(consumables.All(resource => resource.ToCoreDefinition().MaxCharges > 0)).IsTrue();
        AssertThat(equipment.All(resource => resource.ToCoreDefinition().Price >= 0)).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void CanonicalCatalogUsesUniqueContentIdsAndRegisteredUids()
    {
        var catalog = ResourceLoader.Load<GodotResourceCatalog>(GlobalCatalogPath);
        AssertThat(catalog).IsNotNull();
        if (catalog is null)
            return;

        catalog.Validate();
        AssertThat(catalog.Entries.Length is 114 or 115 or 116 or 119 or 123 or 124 or 125).IsTrue();
        AssertThat(catalog.Entries.Select(entry => entry.ContentIdValue).Distinct().Count()).IsEqual(catalog.Entries.Length);
        AssertThat(catalog.Entries.All(entry => ResourceUid.HasId(
            ResourceUid.TextToId(entry.ResourceUidValue)))).IsTrue();
    }
}
