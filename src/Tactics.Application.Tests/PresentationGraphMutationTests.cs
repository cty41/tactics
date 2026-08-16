using NUnit.Framework;
using Tactics.Application.Presentation;

namespace Tactics.Application.Tests;

public sealed class PresentationGraphMutationTests
{
    [Test]
    public void Revision_IsDeterministicAndChangesWithNormalizedContent()
    {
        PresentationGraphDocument first = Document();
        PresentationGraphDocument identical = Document();
        PresentationGraphDocument disabled = new PresentationGraphMutationService().Apply(
            first,
            Change(first, new SetPresentationNodeEnabledOperation("action", false))).Document;
        PresentationGraphDocument moved = new PresentationGraphMutationService().Apply(
            first,
            Change(first, new SetPresentationNodePositionOperation(
                "action",
                new PresentationNodePosition(320f, 40f)))).Document;

        Assert.Multiple(() =>
        {
            Assert.That(first.Revision, Does.Match("^sha256:[a-f0-9]{64}$"));
            Assert.That(identical.Revision, Is.EqualTo(first.Revision));
            Assert.That(disabled.Revision, Is.Not.EqualTo(first.Revision));
            Assert.That(moved.Revision, Is.Not.EqualTo(first.Revision));
        });
    }

    [Test]
    public void Apply_SetPositionIsAtomicAndRejectsInvalidOrOverlappingCoordinates()
    {
        PresentationGraphDocument source = Document();
        PresentationGraphMutationResult moved = new PresentationGraphMutationService().Apply(
            source,
            Change(source, new SetPresentationNodePositionOperation(
                "action",
                new PresentationNodePosition(320f, 40f))));
        PresentationGraphMutationResult overlap = new PresentationGraphMutationService().Apply(
            source,
            Change(source, new SetPresentationNodePositionOperation(
                "action",
                source.Nodes[0].Position)));
        PresentationGraphMutationResult invalid = new PresentationGraphMutationService().Apply(
            source,
            Change(source, new SetPresentationNodePositionOperation(
                "action",
                new PresentationNodePosition(float.NaN, 0f))));

        Assert.Multiple(() =>
        {
            Assert.That(moved.Succeeded, Is.True);
            Assert.That(moved.Document.Nodes.Single(node => node.NodeId == "action").Position,
                Is.EqualTo(new PresentationNodePosition(320f, 40f)));
            Assert.That(source.Nodes.Single(node => node.NodeId == "action").Position,
                Is.EqualTo(new PresentationNodePosition(280f, 0f)));
            Assert.That(overlap.Succeeded, Is.False);
            Assert.That(overlap.Diagnostics.Single().Code, Is.EqualTo("presentation.position_overlap"));
            Assert.That(invalid.Succeeded, Is.False);
            Assert.That(invalid.Diagnostics.Single().Code, Is.EqualTo("presentation.invalid_position"));
        });
    }

    [Test]
    public void AutoLayout_IsDeterministicAndProducesDistinctTopologyLayers()
    {
        PresentationGraphDocument source = Document();
        var service = new PresentationGraphLayoutService();
        IReadOnlyDictionary<string, PresentationNodePosition> first = service.Arrange(source);
        IReadOnlyDictionary<string, PresentationNodePosition> second = service.Arrange(source);
        PresentationGraphMutationResult arranged = new PresentationGraphMutationService().Apply(
            source,
            service.CreateChangeSet(source));

        Assert.Multiple(() =>
        {
            Assert.That(second, Is.EqualTo(first));
            Assert.That(first.Values.Distinct().Count(), Is.EqualTo(source.Nodes.Count));
            Assert.That(first["entry"].X, Is.LessThan(first["action"].X));
            Assert.That(first["action"].X, Is.LessThan(first["finish"].X));
            Assert.That(arranged.Succeeded, Is.True);
        });
    }

    [Test]
    public void SemanticTitles_HideStableIdsAndDisambiguateFinishLanes()
    {
        PresentationGraphDocument source = TwoLaneDocument();
        IReadOnlyDictionary<string, string> titles = new PresentationGraphTitleService().CreateTitles(source);

        Assert.That(titles.Values.ToArray(), Is.EqualTo(new[]
        {
            "Action Entry",
            "Ranged Tween",
            "Action Finish",
            "Projectile Entry",
            "Projectile",
            "Projectile Finish"
        }));
        Assert.That(titles.Values.Any(title => title.Contains("action-entry-id", StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public void Apply_SetEnabledIsAtomicAndLeavesSourceImmutable()
    {
        PresentationGraphDocument source = Document();
        PresentationGraphMutationResult result = new PresentationGraphMutationService().Apply(
            source,
            Change(source, new SetPresentationNodeEnabledOperation("action", false)));

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Changed, Is.True);
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(source.Nodes.Single(node => node.NodeId == "action").Enabled, Is.True);
            Assert.That(result.Document.Nodes.Single(node => node.NodeId == "action").Enabled, Is.False);
        });
    }

    [Test]
    public void Apply_RejectsStaleRevisionWithoutPartialMutation()
    {
        PresentationGraphDocument source = Document();
        var stale = new PresentationGraphChangeSet(
            "test.stale",
            "sha256:" + new string('0', 64),
            new[] { new SetPresentationNodeEnabledOperation("action", false) });
        PresentationGraphMutationResult result = new PresentationGraphMutationService().Apply(source, stale);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Document, Is.SameAs(source));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("presentation.revision_conflict"));
        });
    }

    [Test]
    public void Apply_MultiOperationFailureRollsBackEarlierCandidateChanges()
    {
        PresentationGraphDocument source = Document();
        PresentationGraphMutationResult result = new PresentationGraphMutationService().Apply(
            source,
            Change(
                source,
                new SetPresentationNodeEnabledOperation("action", false),
                new SetPresentationNodeEnabledOperation("missing", false)));

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Document, Is.SameAs(source));
            Assert.That(source.Nodes.Single(node => node.NodeId == "action").Enabled, Is.True);
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("presentation.node_not_found"));
        });
    }

    private static PresentationGraphChangeSet Change(
        PresentationGraphDocument source,
        params PresentationGraphOperation[] operations) =>
        new("test.change", source.Revision, operations);

    private static PresentationGraphDocument Document() => new(
        1,
        new[]
        {
            new PresentationNodeDocument(
                "entry", "entry", "entry", "Action", true, new PresentationNodePosition(0f, 0f)),
            new PresentationNodeDocument(
                "action", "unit.tween.ranged", "leaf", string.Empty, true, new PresentationNodePosition(280f, 0f)),
            new PresentationNodeDocument(
                "finish", "finish", "finish", string.Empty, true, new PresentationNodePosition(560f, 0f))
        },
        new[]
        {
            new PresentationEdgeDocument("edge.1", "entry", "action"),
            new PresentationEdgeDocument("edge.2", "action", "finish")
        });

    private static PresentationGraphDocument TwoLaneDocument() => new(
        1,
        new[]
        {
            Node("action-entry-id", "PresentationEntryNodeRecord", "entry", "Action", 0f, 20f),
            Node("action-id", "PresentationUnitTweenNodeRecord", "leaf", string.Empty, 270f, 20f),
            Node("action-finish-id", "PresentationFinishNodeRecord", "finish", string.Empty, 560f, 20f),
            Node("projectile-entry-id", "PresentationEntryNodeRecord", "entry", "Projectile", 0f, 220f),
            Node("projectile-id", "PresentationProjectileNodeRecord", "leaf", string.Empty, 280f, 220f),
            Node("projectile-finish-id", "PresentationFinishNodeRecord", "finish", string.Empty, 570f, 220f)
        },
        new[]
        {
            new PresentationEdgeDocument("edge.action.1", "action-entry-id", "action-id"),
            new PresentationEdgeDocument("edge.action.2", "action-id", "action-finish-id"),
            new PresentationEdgeDocument("edge.projectile.1", "projectile-entry-id", "projectile-id"),
            new PresentationEdgeDocument("edge.projectile.2", "projectile-id", "projectile-finish-id")
        });

    private static PresentationNodeDocument Node(
        string id,
        string type,
        string kind,
        string cue,
        float x,
        float y) => new(id, type, kind, cue, true, new PresentationNodePosition(x, y));
}
