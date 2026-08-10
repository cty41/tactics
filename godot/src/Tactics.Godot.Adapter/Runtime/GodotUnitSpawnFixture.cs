using Godot;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Units;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>
/// Headless fixture proving that definition identity and instance identity remain separate.
/// </summary>
[GlobalClass]
public partial class GodotUnitSpawnFixture : Node2D
{
	private const int GridSize = 10;
	private const float CellSize = 64f;
	private static readonly Vector2 GridOrigin = new(320f, 40f);
	public static readonly Color PreviewBackgroundColor = new("82909b");
	public static readonly Color PreviewGridColor = new("455865");
	private static readonly GridPoint[] SpawnCells =
	[
		new(0, 0), new(3, 0), new(6, 0), new(9, 0),
		new(1, 3), new(4, 3), new(7, 3),
		new(2, 6), new(5, 6), new(8, 6),
		new(3, 9), new(6, 9)
	];

	[Export] public GodotResourceCatalog? Catalog { get; set; }

	public static IReadOnlyList<GridPoint> FixedSpawnCells => SpawnCells;

	public override void _Ready()
	{
		BuildPreview();
	}

	public override void _Draw()
	{
		DrawRect(new Rect2(Vector2.Zero, new Vector2(1280f, 720f)), PreviewBackgroundColor);
		for (int line = 0; line <= GridSize; line++)
		{
			float offset = line * CellSize;
			DrawLine(
				GridOrigin + new Vector2(offset, 0f),
				GridOrigin + new Vector2(offset, GridSize * CellSize),
				PreviewGridColor,
				1f);
			DrawLine(
				GridOrigin + new Vector2(0f, offset),
				GridOrigin + new Vector2(GridSize * CellSize, offset),
				PreviewGridColor,
				1f);
		}
	}

	public IReadOnlyList<BattleUnitState> CreateStates()
	{
		if (Catalog is null)
			throw new InvalidOperationException("Unit spawn fixture has no ContentCatalog.");
		Catalog.Validate();
		GodotResourceEntry[] entries = Catalog.Entries
			.Where(entry => entry.ResourceTypeIdValue == "unit")
			.OrderBy(entry => entry.ContentIdValue, StringComparer.Ordinal)
			.ToArray();
		return entries.Select((entry, index) =>
		{
			if (!Catalog.TryGet(entry.ContentIdValue, out Resource? loaded) ||
				loaded is not UnitDefinitionResource definition)
			{
				throw new InvalidOperationException($"Unit spawn fixture cannot load '{entry.ContentIdValue}'.");
			}
			return GodotUnitFactory.CreateBattleState(
				definition,
				new UnitInstanceId($"fixture.unit.{index}"),
				SpawnCells[index],
				index < 3 ? 0 : 1,
				index);
		}).ToArray();
	}

	public void BuildPreview()
	{
		if (GetChildren().OfType<GodotUnitActor>().Any())
			return;
		if (Catalog is null)
			throw new InvalidOperationException("Unit spawn fixture has no ContentCatalog.");

		IReadOnlyList<BattleUnitState> states = CreateStates();
		foreach ((BattleUnitState state, int index) in states.Select((state, index) => (state, index)))
		{
			if (!Catalog.TryGet(state.Unit.DefinitionId.Value, out Resource? loaded) ||
				loaded is not UnitDefinitionResource definition)
			{
				throw new InvalidOperationException(
					$"Unit spawn fixture cannot load '{state.Unit.DefinitionId.Value}'.");
			}

			GodotUnitActor actor = GodotUnitFactory.InstantiateActor(definition);
			actor.Name = $"Spawn{index:00}_{definition.SourceId}";
			actor.Position = GridOrigin + new Vector2(
				(state.Unit.Position.X + 0.5f) * CellSize,
				(state.Unit.Position.Y + 0.5f) * CellSize);
			actor.Scale = new Vector2(0.3f, 0.3f);
			actor.SetFacing((GodotUnitFacing)(index % 4));
			AddChild(actor);
		}
		QueueRedraw();
	}
}
