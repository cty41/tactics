using GdUnit4;
using Godot;
using Tactics.Core.Board;
using Tactics.Godot.Adapter.Runtime;
using Tactics.Application.Presentation;
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
    public void BoardFitterCentersCompleteDiamondBoundsInFullGameplaySafeArea()
    {
        Rect2 bounds = GodotBattleBoardFitter.BoardBounds();
        Rect2 safe = new(30, 90, 1540, 650);
        Transform2D fit = GodotBattleBoardFitter.Fit(bounds, safe);
        Rect2 fitted = GodotBattleBoardFitter.TransformBounds(bounds, fit);

        AssertThat(fitted.GetCenter().DistanceTo(safe.GetCenter())).IsLess(0.01f);
        AssertThat(fitted.Position.X).IsGreaterEqual(safe.Position.X - .01f);
        AssertThat(fitted.End.X).IsLessEqual(safe.End.X + .01f);
        AssertThat(fitted.Position.Y).IsGreaterEqual(safe.Position.Y - .01f);
        AssertThat(fitted.End.Y).IsLessEqual(safe.End.Y + .01f);
    }

    [TestCase]
    public void BoardFitterInversePreservesAllGridCenters()
    {
        Transform2D fit = GodotBattleBoardFitter.Fit(GodotBattleBoardFitter.BoardBounds(),
            new Rect2(30, 90, 1540, 650));
        Transform2D inverse = fit.AffineInverse();
        for (int y = 0; y < 10; y++)
        for (int x = 0; x < 10; x++)
        {
            Vector2 local = IsometricBattleBoardLayout.GridToScreen(new GridPoint(x, y));
            AssertThat(inverse * (fit * local)).IsEqualApprox(local, Vector2.One * .001f);
        }
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
    [RequireGodotRuntime]
    public void StandardUnitProfileCarriesFrozenUnityMotionAndContactContract()
    {
        var profile = new StandardUnitPresentationResource();
        AssertThat(profile.MoveCycleDuration).IsEqualApprox(.22f, .0001f);
        AssertThat(profile.MoveTiltDegrees).IsEqualApprox(5f, .0001f);
        AssertThat(profile.MoveLiftPixels).IsEqualApprox(3f, .0001f);
        AssertThat(profile.MoveSwayPixels).IsEqualApprox(3f, .0001f);
        AssertThat(profile.HitShakeDuration).IsEqualApprox(.07f, .0001f);
        AssertThat(profile.HitRecoilPixels).IsEqualApprox(10f, .0001f);
        AssertThat(profile.LethalCollapseScale).IsEqual(new Vector2(1.02f, .58f));
        AssertThat(profile.CorpseStartHeightPixels).IsEqualApprox(8f, .0001f);
        AssertThat(profile.ShadowContactOffsetY).IsLess(0f);
    }

    [TestCase]
    public void PresentationPlayerIncludesMoveSwayHitRecoilAndCorpseLanding()
    {
        string source = File.ReadAllText(Path.Combine("src", "Tactics.Godot.Adapter", "Runtime",
            "GodotBattlePresentationPlayer.cs"));
        AssertThat(source.Contains("PlayMoveSegment", StringComparison.Ordinal)).IsTrue();
        AssertThat(source.Contains("PlayHitReaction", StringComparison.Ordinal)).IsTrue();
        AssertThat(source.Contains("PlayCorpseLanding", StringComparison.Ordinal)).IsTrue();
        AssertThat(source.Contains("LethalCollapseScale", StringComparison.Ordinal)).IsTrue();
        AssertThat(source.Contains("actor.Body, \"scale\"", StringComparison.Ordinal)).IsTrue();
        AssertThat(source.Contains("_rootBaselines", StringComparison.Ordinal)).IsTrue();
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
    public void PresentationRecoveryOnlyRunsForAnUnlockedStalledFrame()
    {
        AssertThat(GodotPlayableRunMain.ShouldRecoverPresentationFrame(true, false, false)).IsTrue();
        AssertThat(GodotPlayableRunMain.ShouldRecoverPresentationFrame(false, false, false)).IsFalse();
        AssertThat(GodotPlayableRunMain.ShouldRecoverPresentationFrame(true, true, false)).IsFalse();
        AssertThat(GodotPlayableRunMain.ShouldRecoverPresentationFrame(true, false, true)).IsFalse();
    }

    [TestCase]
    public void TerminalSettlementWinsAfterTheCommittedPresentationQueueDrains()
    {
        AssertThat((int)GodotPlayableRunMain.ResolvePresentationDrainAction(true, true, false, false))
            .IsEqual((int)GodotPlayableRunMain.PresentationDrainAction.DequeueFrame);
        AssertThat((int)GodotPlayableRunMain.ResolvePresentationDrainAction(false, true, false, false))
            .IsEqual((int)GodotPlayableRunMain.PresentationDrainAction.CompleteBattle);
        AssertThat((int)GodotPlayableRunMain.ResolvePresentationDrainAction(false, false, false, false))
            .IsEqual((int)GodotPlayableRunMain.PresentationDrainAction.Refresh);
        AssertThat((int)GodotPlayableRunMain.ResolvePresentationDrainAction(false, true, true, false))
            .IsEqual((int)GodotPlayableRunMain.PresentationDrainAction.CompleteBattle);
        AssertThat((int)GodotPlayableRunMain.ResolvePresentationDrainAction(true, true, true, false))
            .IsEqual((int)GodotPlayableRunMain.PresentationDrainAction.Pause);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task EmptyPresentationFrameCompletesExactlyOnce()
    {
        var player = new GodotBattlePresentationPlayer();
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        var completions = new List<PresentationFrameCompletion>();
        player.FrameCompleted += completions.Add;

        player.Play(new BattlePresentationFrame("Decision", null!, null!, [], []),
            new Dictionary<Tactics.Core.Units.UnitInstanceId, GodotUnitActor>());
        await player.ToSignal(player.GetTree(), SceneTree.SignalName.ProcessFrame);
        await player.ToSignal(player.GetTree(), SceneTree.SignalName.ProcessFrame);

        AssertThat(completions.Count).IsEqual(1);
        AssertThat(completions[0].Stage).IsEqual("Decision");
        AssertThat(completions[0].Recovered).IsFalse();
        AssertThat(player.HasPendingFrame).IsFalse();
        player.QueueFree();
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
        AssertThat(catalog.Entries.Length).IsEqual(131);
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
        AssertThat(catalog.Entries.Length).IsEqual(131);
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
