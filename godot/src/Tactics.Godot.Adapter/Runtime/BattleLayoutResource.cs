using Godot;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Encounters;

namespace Tactics.Godot.Adapter.Runtime;

[GlobalClass]
public partial class BattleLayoutResource : Resource
{
    [Export] public int SchemaVersion { get; set; }=1;
    [Export] public string ContentIdValue { get; set; }=string.Empty;
    [Export] public string PartySpawnsValue { get; set; }=string.Empty;
    [Export] public string EnemySpawnsValue { get; set; }=string.Empty;
    [Export] public string BlockedCellsValue { get; set; }=string.Empty;

    public BattleLayoutDefinition ToCoreDefinition() => new(
        new ContentId(ContentIdValue), ParseCells(PartySpawnsValue), ParseCells(EnemySpawnsValue), ParseCells(BlockedCellsValue));

    private static GridPoint[] ParseCells(string value) => string.IsNullOrWhiteSpace(value)
        ? Array.Empty<GridPoint>()
        : value.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(item =>
        {
            string[] parts = item.Split(',');
            return new GridPoint(int.Parse(parts[0]), int.Parse(parts[1]));
        }).ToArray();
}
