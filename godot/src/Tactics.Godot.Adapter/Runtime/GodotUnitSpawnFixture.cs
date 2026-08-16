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
	internal const int GridSize = 10;
	internal const float CellSize = 72f;
	internal const float ActorScale = 0.375f;
	internal const float ViewportSafeInset = 24f;
	internal const float BoardSafeInset = 8f;
	internal const string LayoutContract = "native-1600x900-spawn-v1";
	internal const string OverflowPolicy =
		"internal-grid-overflow-allowed; board-frame-and-viewport-clipping-forbidden";
	internal static readonly Vector2 GridOrigin = new(440f, 90f);
	public static readonly Color PreviewBackgroundColor = new("82909b");
	public static readonly Color PreviewGridColor = new("455865");
	private static readonly GridPoint[] SpawnCells =
	[
		new(1, 1), new(3, 1), new(6, 1), new(8, 1),
		new(1, 3), new(4, 3), new(7, 3),
		new(2, 6), new(5, 6), new(8, 6),
		new(3, 8), new(6, 8)
	];

	[Export] public GodotResourceCatalog? Catalog { get; set; }

	public static IReadOnlyList<GridPoint> FixedSpawnCells => SpawnCells;
	internal static Rect2 GridRect => new(
		GridOrigin,
		new Vector2(GridSize * CellSize, GridSize * CellSize));
	internal static Rect2 ViewportVisualSafeRect => new(
		ViewportSafeInset,
		ViewportSafeInset,
		UnitPreviewLayout.CanvasWidth - ViewportSafeInset * 2f,
		UnitPreviewLayout.CanvasHeight - ViewportSafeInset * 2f);
	internal static Rect2 BoardVisualSafeRect => new(
		GridOrigin + new Vector2(BoardSafeInset, BoardSafeInset),
		GridRect.Size - new Vector2(BoardSafeInset * 2f, BoardSafeInset * 2f));

	public override void _Ready()
	{
		BuildPreview();
	}

	public override void _Draw()
	{
		DrawRect(UnitPreviewLayout.CanvasRect, PreviewBackgroundColor);
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
		GodotUnitActor[] existingActors = GetChildren().OfType<GodotUnitActor>().ToArray();
		if (existingActors.Length > 0)
		{
			ValidatePreviewBounds(existingActors);
			return;
		}
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
			actor.Position = GetCellCenter(state.Unit.Position);
			actor.Scale = new Vector2(ActorScale, ActorScale);
			actor.SetFacing((GodotUnitFacing)(index % 4));
			AddChild(actor);
		}
		ValidatePreviewBounds(GetChildren().OfType<GodotUnitActor>());
		QueueRedraw();
	}

	internal static Vector2 GetCellCenter(GridPoint cell)
	{
		return GridOrigin + new Vector2(
			(cell.X + 0.5f) * CellSize,
			(cell.Y + 0.5f) * CellSize);
	}

	internal static Rect2 ComputeActorVisualBounds(GodotUnitActor actor)
	{
		ArgumentNullException.ThrowIfNull(actor);
		if (actor.Body is null || actor.Shadow is null)
			throw new InvalidOperationException("Spawn preview actor is missing Body or Shadow.");
		Rect2 bodyBounds = ComputeSpriteBounds(actor, actor.Body);
		Rect2 shadowBounds = ComputeSpriteBounds(actor, actor.Shadow);
		Vector2 minimum = new(
			Mathf.Min(bodyBounds.Position.X, shadowBounds.Position.X),
			Mathf.Min(bodyBounds.Position.Y, shadowBounds.Position.Y));
		Vector2 bodyEnd = bodyBounds.Position + bodyBounds.Size;
		Vector2 shadowEnd = shadowBounds.Position + shadowBounds.Size;
		Vector2 maximum = new(
			Mathf.Max(bodyEnd.X, shadowEnd.X),
			Mathf.Max(bodyEnd.Y, shadowEnd.Y));
		return new Rect2(minimum, maximum - minimum);
	}

	internal static void ValidatePreviewBounds(IEnumerable<GodotUnitActor> actors)
	{
		ArgumentNullException.ThrowIfNull(actors);
		foreach (GodotUnitActor actor in actors)
		{
			Rect2 bounds = ComputeActorVisualBounds(actor);
			ValidateBounds(actor, bounds, BoardVisualSafeRect, "board frame");
			ValidateBounds(actor, bounds, ViewportVisualSafeRect, "viewport");
		}
	}

	private static void ValidateBounds(
		GodotUnitActor actor,
		Rect2 bounds,
		Rect2 safeRect,
		string boundaryName)
	{
		Vector2 boundsEnd = bounds.Position + bounds.Size;
		Vector2 safeEnd = safeRect.Position + safeRect.Size;
		if (bounds.Position.X < safeRect.Position.X ||
			bounds.Position.Y < safeRect.Position.Y ||
			boundsEnd.X > safeEnd.X ||
			boundsEnd.Y > safeEnd.Y)
		{
			throw new InvalidOperationException(
				$"Spawn preview actor '{actor.Name}' exceeds the {boundaryName} safe rect: {bounds}.");
		}
	}

	private static Rect2 ComputeSpriteBounds(GodotUnitActor actor, Sprite2D sprite)
	{
		if (sprite.Texture is null)
			throw new InvalidOperationException($"Spawn preview sprite '{sprite.Name}' has no texture.");
		if (!sprite.Centered || sprite.RegionEnabled || sprite.Hframes != 1 || sprite.Vframes != 1 ||
			!Mathf.IsZeroApprox(actor.Rotation) || !Mathf.IsZeroApprox(sprite.Rotation))
		{
			throw new InvalidOperationException(
				$"Spawn preview sprite '{sprite.Name}' uses unsupported bounds geometry.");
		}

		Vector2 combinedScale = new(
			actor.Scale.X * sprite.Scale.X,
			actor.Scale.Y * sprite.Scale.Y);
		Vector2 absoluteScale = new(Mathf.Abs(combinedScale.X), Mathf.Abs(combinedScale.Y));
		Vector2 center = actor.Position + new Vector2(
			actor.Scale.X * sprite.Position.X + combinedScale.X * sprite.Offset.X,
			actor.Scale.Y * sprite.Position.Y + combinedScale.Y * sprite.Offset.Y);
		Vector2 size = sprite.Texture.GetSize() * absoluteScale;
		return new Rect2(center - size * 0.5f, size);
	}
}
