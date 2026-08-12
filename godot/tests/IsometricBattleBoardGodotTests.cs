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
        AssertThat(IsometricBattleBoardLayout.GridToScreen(new GridPoint(0, 0))).IsEqual(new Vector2(550f, 601f));
        AssertThat(IsometricBattleBoardLayout.GridToScreen(new GridPoint(9, 0))).IsEqual(new Vector2(982f, 385f));
        AssertThat(IsometricBattleBoardLayout.GridToScreen(new GridPoint(0, 9))).IsEqual(new Vector2(118f, 385f));
        AssertThat(IsometricBattleBoardLayout.GridToScreen(new GridPoint(9, 9))).IsEqual(new Vector2(550f, 169f));
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
    public void PartyProjectsBelowEnemiesWithoutChangingLogicalSpawns()
    {
        Vector2 party=IsometricBattleBoardLayout.GridToScreen(new GridPoint(1,4));
        Vector2 enemy=IsometricBattleBoardLayout.GridToScreen(new GridPoint(7,4));
        AssertThat(party.Y).IsGreater(enemy.Y);
        AssertThat(party.X).IsLess(enemy.X);
    }

    [TestCase]
    public void PresentationFacingMatchesFrozenUnityRules()
    {
        AssertThat(GodotPresentationFacingResolver.Initial(0)).IsEqual(GodotUnitFacing.East);
        AssertThat(GodotPresentationFacingResolver.Initial(1)).IsEqual(GodotUnitFacing.West);
        AssertThat(GodotPresentationFacingResolver.Resolve(new GridPoint(0,0),new GridPoint(1,3),GodotUnitFacing.East)).IsEqual(GodotUnitFacing.North);
        AssertThat(GodotPresentationFacingResolver.Resolve(new GridPoint(2,2),new GridPoint(1,1),GodotUnitFacing.West)).IsEqual(GodotUnitFacing.West);
    }

    [TestCase]
    public void TargetingFacingUsesFirstMoveStepAndSkillTarget()
    {
        GridPoint origin = new(1, 4);
        AssertThat(GodotPresentationFacingResolver.PreviewMove(origin,
            new[] { new GridPoint(2, 4), new GridPoint(3, 4) }, GodotUnitFacing.North)).IsEqual(GodotUnitFacing.East);
        AssertThat(GodotPresentationFacingResolver.PreviewTarget(origin,
            new GridPoint(1, 2), GodotUnitFacing.East)).IsEqual(GodotUnitFacing.South);
    }

    [TestCase]
    public void MultiCellMoveDurationExceedsLegacyTimerAndMustSerializeAttack()
    {
        AssertThat(GodotBattlePresentationPlayer.EstimateMoveDuration(3, .22d, .06d)).IsGreater(.45d);
    }

    [TestCase]
    public void PlaybackSpeedSupportsUnityCycleValues()
    {
        AssertThat(GodotBattlePresentationPlayer.IsSupportedSpeed(.5f)).IsTrue();
        AssertThat(GodotBattlePresentationPlayer.IsSupportedSpeed(1f)).IsTrue();
        AssertThat(GodotBattlePresentationPlayer.IsSupportedSpeed(2f)).IsTrue();
        AssertThat(GodotBattlePresentationPlayer.IsSupportedSpeed(4f)).IsTrue();
        AssertThat(GodotBattlePresentationPlayer.IsSupportedSpeed(.75f)).IsFalse();
    }

    [TestCase]
    public void ProgrammaticFxIsHiddenUntilItsReleaseCallback()
    {
        string source=File.ReadAllText(Path.Combine("src","Tactics.Godot.Adapter","Runtime",
            "GodotBattlePresentationPlayer.cs"));
        int hidden=source.IndexOf("Visible=false",StringComparison.Ordinal);
        int release=source.IndexOf("fx.Visible=true",StringComparison.Ordinal);
        int travel=source.IndexOf("\"Progress\",1f",StringComparison.Ordinal);
        AssertThat(hidden).IsGreaterEqual(0);
        AssertThat(release).IsGreater(hidden);
        AssertThat(travel).IsGreater(release);
    }

    [TestCase]
    public void BaseTilesAlternateWarmAndCoolProjectPalette()
    {
        Color first = GodotIsometricBattleBoard.BaseTileColor(new GridPoint(0, 0), false);
        Color neighbor = GodotIsometricBattleBoard.BaseTileColor(new GridPoint(1, 0), false);
        AssertThat(first).IsNotEqual(neighbor);
        AssertThat(GodotIsometricBattleBoard.BaseTileColor(new GridPoint(2, 0), false)).IsEqual(first);
    }

    [TestCase]
    public void BattleBackdropUsesProjectOwnedGradientContract()
    {
        AssertThat(GodotBattleBackdrop.ShaderCode.Contains("vignette_strength", StringComparison.Ordinal)).IsTrue();
        AssertThat(GodotBattleBackdrop.ShaderCode.Contains("noise_strength", StringComparison.Ordinal)).IsTrue();
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
        AssertThat(catalog.Entries.Length).IsEqual(124);
        AssertThat(catalog.Entries.Count(entry => entry.ContentIdValue == "battle-board.pure-run.isometric-v1")).IsEqual(1);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ProgrammaticSkillProfilesExcludeThirdPartyPayload()
    {
        string[] paths=["FireballPresentation.tres","BoneSpearPresentation.tres","ThrustPresentation.tres","IceBoltPresentation.tres","LightningPresentation.tres","PoisonSpearPresentation.tres","AmplifyDamagePresentation.tres"];
        SkillPresentationResource[] profiles=paths.Select(name=>ResourceLoader.Load<SkillPresentationResource>($"res://content/presentation/{name}")!).ToArray();
        AssertThat(profiles.All(value=>value is not null)).IsTrue();
        AssertThat(profiles.All(value=>value.PayloadBoundary=="programmatic-only-no-piloto-payload")).IsTrue();
        AssertThat(profiles.Single(value=>value.ProgrammaticKind=="fireball").LevelOneHasAreaEffect).IsFalse();
        AssertThat(profiles.Single(value=>value.ProgrammaticKind=="bone-spear").MaximumGhosts).IsEqual(2);
        var catalog=ResourceLoader.Load<GodotResourceCatalog>("res://content/ContentCatalog.tres")!;
        AssertThat(catalog.Entries.Length).IsEqual(124);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void StatusProfileRemainsAndNonUnityBoardCameraProfileIsRemoved()
    {
        var status=ResourceLoader.Load<StatusPresentationResource>("res://content/presentation/StandardStatusPresentationV1.tres");
        var catalog=ResourceLoader.Load<GodotResourceCatalog>("res://content/ContentCatalog.tres");
        AssertThat(status).IsNotNull();AssertThat(catalog).IsNotNull();if(status is null||catalog is null)return;
        AssertThat(status.MaximumVisibleStatuses).IsEqual(4);
        AssertThat(catalog.Entries.Any(entry=>entry.ContentIdValue=="presentation.camera.battle-focus-v1")).IsFalse();
    }
}
