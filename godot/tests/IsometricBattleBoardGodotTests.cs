using GdUnit4;
using Godot;
using Tactics.Core.Board;
using Tactics.Godot.Adapter.Runtime;
using static GdUnit4.Assertions;

namespace Tactics.Godot.Tests;

[TestSuite]
public sealed class IsometricBattleBoardGodotTests
{
    [TestCase]
    public void GridProjectionRoundTripsAllCells()
    {
        for (int y = 0; y < IsometricBattleBoardLayout.GridSize; y++)
        for (int x = 0; x < IsometricBattleBoardLayout.GridSize; x++)
        {
            GridPoint expected = new(x, y);
            bool found = IsometricBattleBoardLayout.TryScreenToGrid(IsometricBattleBoardLayout.GridToScreen(expected), out GridPoint actual);
            AssertThat(found).IsTrue();
            AssertThat(actual).IsEqual(expected);
        }
    }

    [TestCase]
    public void ProjectionMatchesNativeCanvasContract()
    {
        AssertThat(IsometricBattleBoardLayout.GridToScreen(new GridPoint(0, 0))).IsEqual(new Vector2(550f, 169f));
        AssertThat(IsometricBattleBoardLayout.GridToScreen(new GridPoint(9, 0))).IsEqual(new Vector2(982f, 385f));
        AssertThat(IsometricBattleBoardLayout.GridToScreen(new GridPoint(0, 9))).IsEqual(new Vector2(118f, 385f));
        AssertThat(IsometricBattleBoardLayout.GridToScreen(new GridPoint(9, 9))).IsEqual(new Vector2(550f, 601f));
    }

    [TestCase]
    public void OutsideAndSharedEdgePickingIsDeterministic()
    {
        AssertThat(IsometricBattleBoardLayout.TryScreenToGrid(new Vector2(20, 20), out _)).IsFalse();
        Vector2 sharedEdge = IsometricBattleBoardLayout.GridToScreen(new GridPoint(2, 2)) + new Vector2(48, 0);
        AssertThat(IsometricBattleBoardLayout.TryScreenToGrid(sharedEdge, out GridPoint selected)).IsTrue();
        AssertThat(selected).IsEqual(new GridPoint(2, 1));
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GeneratedBoardResourceAndCatalogAreValid()
    {
        var board = ResourceLoader.Load<IsometricBattleBoardResource>("res://content/presentation/BattleBoardPureRunIsometricV1.tres");
        var catalog = ResourceLoader.Load<GodotResourceCatalog>("res://content/ContentCatalog.tres");
        AssertThat(board).IsNotNull();
        AssertThat(catalog).IsNotNull();
        if (board is null || catalog is null) return;
        AssertThat(board.TileSize).IsEqual(new Vector2(96, 48));
        AssertThat(catalog.Entries.Length is 115 or 116 or 119).IsTrue();
        AssertThat(catalog.Entries.Count(entry => entry.ContentIdValue == "battle-board.pure-run.isometric-v1")).IsEqual(1);
    }
}
