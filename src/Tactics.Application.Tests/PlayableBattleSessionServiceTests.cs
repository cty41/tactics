using NUnit.Framework;
using Tactics.Application.Battle;
using Tactics.Application.Runs;
using Tactics.Core.AI;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Skills;
using Tactics.Core.Units;

namespace Tactics.Application.Tests;

public sealed class PlayableBattleSessionServiceTests
{
    [Test]
    public void TargetingCancelAndIllegalCell_DoNotMutateBattleState()
    {
        PlayableBattleSessionService service = CreateService(out _, out _);
        BattleState original = service.State;

        Assert.That(service.Submit(new BeginMoveIntent()).Snapshot.TargetingMode, Is.EqualTo(BattleTargetingMode.Move));
        Assert.That(service.Submit(new CancelTargetingIntent()).Snapshot.TargetingMode, Is.EqualTo(BattleTargetingMode.None));
        service.Submit(new BeginMoveIntent());
        BattleUiIntentResult rejected = service.Submit(new ConfirmCellIntent(new GridPoint(9, 9)));

        Assert.Multiple(() =>
        {
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.FailureCode, Is.Not.Empty);
            Assert.That(service.State, Is.SameAs(original));
        });
    }

    [Test]
    public void SkillIntent_UsesCanonicalTransitionAndCreatesSingleRunResult()
    {
        PlayableBattleSessionService service = CreateService(out SkillDefinition playerSkill, out UnitInstanceId enemyId);
        GridPoint targetCell = service.State.Units[enemyId].Unit.Position;

        Assert.That(service.Submit(new SelectSkillIntent(playerSkill.ContentId)).Succeeded, Is.True);
        BattleUiIntentResult result = service.Submit(new ConfirmCellIntent(targetCell));
        BattleUiIntentResult duplicate = service.Submit(new ConfirmCellIntent(targetCell));

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Snapshot.Phase, Is.EqualTo(PlayableBattlePhase.Victory));
            Assert.That(result.BattleResult, Is.Not.Null);
            Assert.That(result.BattleResult!.PlayerVictory, Is.True);
            Assert.That(result.Events.OfType<DamageAppliedEvent>().Count(), Is.EqualTo(1));
            Assert.That(duplicate.Succeeded, Is.False);
            Assert.That(service.BattleResult, Is.SameAs(result.BattleResult));
        });
    }

    [Test]
    public void SkillPreview_SeparatesGeometricRangeFromLegalTargetsWithoutMutatingState()
    {
        PlayableBattleSessionService service = CreateService(out SkillDefinition playerSkill, out UnitInstanceId enemyId);
        BattleState original = service.State;
        GridPoint emptyInRange = new(1, 2);
        GridPoint enemyCell = service.State.Units[enemyId].Unit.Position;

        BattleUiIntentResult selected = service.Submit(new SelectSkillIntent(playerSkill.ContentId));
        BattleUiSkillPreview? skillPreview = selected.Snapshot.SkillPreview;
        BattleUiImpactPreview? emptyPreview = service.PreviewSkillTarget(emptyInRange);
        BattleUiImpactPreview? enemyPreview = service.PreviewSkillTarget(enemyCell);

        Assert.Multiple(() =>
        {
            Assert.That(skillPreview, Is.Not.Null);
            Assert.That(skillPreview!.RangeCells, Does.Contain(emptyInRange));
            Assert.That(skillPreview.LegalTargets.Select(target => target.Cell), Does.Not.Contain(emptyInRange));
            Assert.That(skillPreview.LegalTargets.Select(target => target.Cell), Does.Contain(enemyCell));
            Assert.That(emptyPreview!.IsInRange, Is.True);
            Assert.That(emptyPreview.IsLegal, Is.False);
            Assert.That(emptyPreview.FailureCode, Is.EqualTo("no_valid_target"));
            Assert.That(enemyPreview!.IsLegal, Is.True);
            Assert.That(enemyPreview.PrimaryImpactUnitId, Is.EqualTo(enemyId));
            Assert.That(enemyPreview.ImpactCells, Does.Contain(enemyCell));
            Assert.That(service.State, Is.SameAs(original));
            Assert.That(service.State.RandomState, Is.EqualTo(original.RandomState));
        });
    }

    [Test]
    public void SkillPreview_ShowsRangeWhenNoLegalTargetExists()
    {
        PlayableBattleSessionService service = CreateService(out SkillDefinition playerSkill, out _, enemyCell: new GridPoint(9, 9));

        BattleUiSkillPreview? preview = service.Submit(new SelectSkillIntent(playerSkill.ContentId)).Snapshot.SkillPreview;

        Assert.Multiple(() =>
        {
            Assert.That(preview, Is.Not.Null);
            Assert.That(preview!.RangeCells, Is.Not.Empty);
            Assert.That(preview.LegalTargets, Is.Empty);
        });
    }

    [Test]
    public void ThrustPreview_OnlyIncludesAxialCells()
    {
        PlayableBattleSessionService service = CreateService(
            out SkillDefinition playerSkill,
            out _,
            executionKind: SkillExecutionKind.Thrust);

        BattleUiSkillPreview? preview = service.Submit(new SelectSkillIntent(playerSkill.ContentId)).Snapshot.SkillPreview;

        Assert.Multiple(() =>
        {
            Assert.That(preview, Is.Not.Null);
            Assert.That(preview!.RangeCells, Does.Contain(new GridPoint(1, 2)));
            Assert.That(preview.RangeCells, Does.Contain(new GridPoint(2, 1)));
            Assert.That(preview.RangeCells, Does.Not.Contain(new GridPoint(2, 2)));
        });
    }

    [Test]
    public void EndTurn_AutomaticallyExecutesEnemyAndReturnsToPlayer()
    {
        PlayableBattleSessionService service = CreateService(out _, out _);

        BattleUiIntentResult result = service.Submit(new EndTurnIntent());

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Snapshot.Phase, Is.EqualTo(PlayableBattlePhase.PlayerTurn));
            Assert.That(result.Snapshot.Round, Is.EqualTo(2));
            Assert.That(result.Snapshot.RecentEvents.OfType<SkillUsedEvent>(), Is.Not.Empty);
            Assert.That(service.HasPendingAutomaticFrames,Is.True);
        });
        var frames=new List<BattleUiFrame>();while(service.DequeueAutomaticFrame() is { } frame)frames.Add(frame);
        Assert.That(frames.Select(frame=>frame.Stage),Does.Contain("Decision").And.Contain("Skill").And.Contain("EndTurn"));
    }

    [Test]
    public void PlayerPresentationAfter_DoesNotContainFutureAiTurnResults()
    {
        PlayableBattleSessionService service = CreateService(out _, out UnitInstanceId enemyId);
        UnitInstanceId playerId = service.State.Units.Values.Single(unit => unit.Unit.PlayerNumber == 0).Unit.InstanceId;
        int playerHealthBefore = service.State.Units[playerId].CurrentHealth;

        BattleUiIntentResult result = service.Submit(new EndTurnIntent());

        Assert.Multiple(() =>
        {
            Assert.That(result.Presentation, Is.Not.Null);
            Assert.That(result.Presentation!.After.ActiveUnitId, Is.EqualTo(enemyId));
            Assert.That(result.Presentation.After.Units.Single(unit => unit.UnitId == playerId).CurrentHealth,
                Is.EqualTo(playerHealthBefore));
            Assert.That(result.Snapshot.ActiveUnitId, Is.EqualTo(playerId),
                "The authoritative session may already have drained AI turns, but the player frame must remain chronological.");
        });
    }

    [Test]
    public void DefeatedActiveUnit_IsSkippedThroughCanonicalEndTurn()
    {
        PlayableBattleSessionService service = CreateService(out _, out _, defeatedLeader: true);

        Assert.Multiple(() =>
        {
            Assert.That(service.CaptureSnapshot().Phase, Is.EqualTo(PlayableBattlePhase.PlayerTurn));
            Assert.That(service.CaptureSnapshot().RecentEvents.OfType<TurnAdvancedEvent>().Count(), Is.EqualTo(1));
        });
    }

    private static PlayableBattleSessionService CreateService(
        out SkillDefinition playerSkill,
        out UnitInstanceId enemyId,
        bool defeatedLeader = false,
        GridPoint? enemyCell = null,
        SkillExecutionKind executionKind = SkillExecutionKind.Fireball)
    {
        var playerId = new UnitInstanceId("party-mage");
        enemyId = new UnitInstanceId("enemy-goat");
        var leaderId = new UnitInstanceId("party-leader");
        playerSkill = Skill("skill.mage.fireball.lv1", 50, executionKind);
        SkillDefinition enemySkill = Skill("skill.basic.melee", 1);
        var cells = new Dictionary<GridPoint, CellState>();
        for (int x = 0; x < 10; x++)
        for (int y = 0; y < 10; y++)
            cells[new GridPoint(x, y)] = new CellState();
        var player = Unit(playerId, "unit.pure-run.mage", new GridPoint(1, 1), 0, 0, 20, 20);
        var enemy = Unit(enemyId, "unit.pure-run.goat-charger", enemyCell ?? new GridPoint(2, 1), 1, 1, 20, 20);
        var units = new List<BattleUnitState>();
        var order = new List<UnitInstanceId>();
        if (defeatedLeader)
        {
            units.Add(Unit(leaderId, "unit.pure-run.amazon", new GridPoint(0, 1), 0, 0, 20, 0));
            order.Add(leaderId);
        }
        units.Add(player);
        units.Add(enemy);
        order.Add(playerId);
        order.Add(enemyId);
        var state = new BattleState(new BoardSnapshot(cells), units, order, randomState: 7);
        var ai = new AiDefinition(
            new ContentId("ai.pure-run.charger"), AiArchetype.Charger,
            new AiProfileDefinition(1, 1, 0, 0), new[] { enemySkill.ContentId }, Array.Empty<ContentId>());
        var request = new EncounterRequest("run-test", 2, new ContentId("encounter.pure-run.n1"), Array.Empty<RunCharacterState>());
        var context = new PlayableBattleSessionContext(
            state, 0,
            new Dictionary<UnitInstanceId, IReadOnlyList<SkillDefinition>> { [playerId] = new[] { playerSkill } },
            new Dictionary<UnitInstanceId, AiDefinition> { [enemyId] = ai },
            new Dictionary<ContentId, SkillDefinition> { [playerSkill.ContentId] = playerSkill, [enemySkill.ContentId] = enemySkill },
            request,
            new Dictionary<UnitInstanceId, string> { [playerId] = "pure_run_mage" });
        return new PlayableBattleSessionService(context);
    }

    private static SkillDefinition Skill(string id, int damage, SkillExecutionKind? executionKind = null) => new(
        new ContentId(id), id, SkillRole.Any, SkillKind.Active, 1, 0, 1, 4,
        executionKind ?? (id.Contains("fireball", StringComparison.Ordinal) ? SkillExecutionKind.Fireball : SkillExecutionKind.MeleeAttack),
        damage, SkillDamageKind.Magical);

    private static BattleUnitState Unit(
        UnitInstanceId id, string definitionId, GridPoint cell, int player, int ordinal, int maxHealth, int health) =>
        new(new UnitState(id, new ContentId(definitionId), cell, 3, 10 - ordinal, player, ordinal),
            maxHealth, health, maxMana: 10, currentMana: 10, physicalAttack: 1, magicalAttack: 1);
}
