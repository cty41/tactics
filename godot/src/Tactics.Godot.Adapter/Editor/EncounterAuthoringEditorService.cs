#if TOOLS
using Tactics.Application.Authoring;
using Tactics.Core.Encounters;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

public static class EncounterAuthoringEditorService
{
    public static EncounterAuthoringDocument Read(EncounterDefinitionResource value) => new(value.ContentIdValue,
        value.LayoutContentId, value.MonsterUnitContentIds, value.MonsterAiContentIds, value.HealthMultiplier,
        value.OutputMultiplier, value.MinimumStartingMana, Enum.Parse<EncounterClass>(value.EncounterClassValue));
    public static void Write(EncounterDefinitionResource value, EncounterAuthoringDocument document)
    {
        if (value.ContentIdValue != document.ContentId) throw new InvalidOperationException("Encounter identity differs.");
        _ = document.ToCoreDefinition(); value.LayoutContentId = document.LayoutContentId;
        value.MonsterUnitContentIds = document.MonsterUnitContentIds.ToArray(); value.MonsterAiContentIds = document.MonsterAiContentIds.ToArray();
        value.HealthMultiplier = document.HealthMultiplier; value.OutputMultiplier = document.OutputMultiplier;
        value.MinimumStartingMana = document.MinimumStartingMana; value.EncounterClassValue = document.EncounterClass.ToString();
    }
    public static BattleLayoutAuthoringDocument Read(BattleLayoutResource value) => new(value.ContentIdValue,
        Parse(value.PartySpawnsValue), Parse(value.EnemySpawnsValue), Parse(value.BlockedCellsValue),
        Parse(value.ShallowWaterCellsValue), value.SchemaVersion);
    public static void Write(BattleLayoutResource value, BattleLayoutAuthoringDocument document)
    {
        if (value.ContentIdValue != document.ContentId) throw new InvalidOperationException("Layout identity differs."); document.Validate();
        value.SchemaVersion = document.SchemaVersion; value.PartySpawnsValue = Format(document.PartySpawns); value.EnemySpawnsValue = Format(document.EnemySpawns); value.BlockedCellsValue = Format(document.BlockedCells); value.ShallowWaterCellsValue = Format(document.ShallowWaterCells);
    }
    private static GridCellAuthoring[] Parse(string value) => string.IsNullOrWhiteSpace(value) ? Array.Empty<GridCellAuthoring>() : value.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(cell => { string[] parts = cell.Split(','); return new GridCellAuthoring(int.Parse(parts[0]), int.Parse(parts[1])); }).ToArray();
    private static string Format(IEnumerable<GridCellAuthoring> cells) => string.Join(';', cells.Select(value => $"{value.X},{value.Y}"));
}
#endif
