using NUnit.Framework;
using Tactics.Application.Authoring;
using Tactics.Core.Runs;
using Tactics.Core.Encounters;
using Tactics.Core.AI;
using Tactics.Core.Content;
using Tactics.Core.Skills;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Units;

namespace Tactics.Application.Tests.Authoring;

public sealed class AuthoringKernelTests
{
    [Test]
    public void SkillBattlePreview_UsesRealTransitionAndDoesNotMutateSourceState()
    {
        var actorId = new UnitInstanceId("preview.actor");
        var enemyId = new UnitInstanceId("preview.enemy");
        BattleState state = BattleStateForPreview(actorId, enemyId);
        SkillDefinition skill = Skill("skill.preview", SkillExecutionKind.MeleeAttack, manaCost: 2, damage: 5);
        var context = new SkillBattlePreviewContext("encounter.preview", actorId.Value, enemyId.Value,
            new GridCellAuthoring(2, 1), 42);

        SkillBattlePreviewResult result = new SkillBattlePreviewService().Preview(state, skill, context);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.RejectionReason, Is.Null);
            Assert.That(result.BeforeFingerprint, Is.EqualTo(SkillBattlePreviewService.Fingerprint(state)));
            Assert.That(result.AfterFingerprint, Is.Not.EqualTo(result.BeforeFingerprint));
            Assert.That(result.SourceStateUnchanged, Is.True);
            Assert.That(result.Events, Has.Some.StartsWith("SkillUsedEvent:"));
            Assert.That(result.Values["manaSpent"], Is.EqualTo("2"));
        });
    }

    [Test]
    public void SkillBattlePreview_ReturnsCanonicalRejectionForIllegalTarget()
    {
        var actorId = new UnitInstanceId("preview.actor");
        var enemyId = new UnitInstanceId("preview.enemy");
        BattleState state = BattleStateForPreview(actorId, enemyId);
        SkillDefinition skill = Skill("skill.preview", SkillExecutionKind.MeleeAttack, manaCost: 2, damage: 5);
        var context = new SkillBattlePreviewContext("encounter.preview", actorId.Value, "missing.target",
            new GridCellAuthoring(9, 9), 42);

        SkillBattlePreviewResult result = new SkillBattlePreviewService().Preview(state, skill, context);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.RejectionReason, Is.EqualTo("no_valid_target"));
            Assert.That(result.BeforeFingerprint, Is.EqualTo(result.AfterFingerprint));
            Assert.That(result.SourceStateUnchanged, Is.True);
        });
    }

    [Test]
    public void BatchChangeSet_RequiresDeleteSnapshotAndTypedRebindDocuments()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => new AuthoringBatchChangeSet("delete", Array.Empty<AuthoringDocumentChange>(),
                [new AuthoringAssetChange(AuthoringAssetChangeKind.Delete, "event.test")]),
                Throws.ArgumentException.With.Message.Contains("ExpectedReferenceRevision"));
            Assert.That(() => new AuthoringBatchChangeSet("rebind", Array.Empty<AuthoringDocumentChange>(),
                [new AuthoringAssetChange(AuthoringAssetChangeKind.Rebind, "event.test")]),
                Throws.ArgumentException.With.Message.Contains("typed document changes"));
            Assert.That(() => new AuthoringBatchChangeSet("duplicate-lifecycle", Array.Empty<AuthoringDocumentChange>(),
                [
                    new AuthoringAssetChange(AuthoringAssetChangeKind.Create, "event.test", ResourceType: "event"),
                    new AuthoringAssetChange(AuthoringAssetChangeKind.Delete, "event.test", ExpectedReferenceRevision: "sha256:test")
                ]), Throws.ArgumentException.With.Message.Contains("multiple lifecycle operations"));
        });
    }

    [Test]
    public void Session_TracksDraftRevertConflictAndAppliedRevision()
    {
        MapAuthoringDocument source = Map("map.test", "start");
        var session = new AuthoringSession<MapAuthoringDocument>(AuthoringDocumentKind.Map, source);
        MapAuthoringDocument changed = Map("map.test", "renamed");

        session.ReplaceDraft(changed);
        Assert.That(session.IsDirty, Is.True);
        Assert.That(session.HasExternalConflict(Map("map.test", "external")), Is.True);
        session.Revert();
        Assert.That(session.IsDirty, Is.False);
        session.AcceptApplied(changed);
        Assert.Multiple(() =>
        {
            Assert.That(session.IsDirty, Is.False);
            Assert.That(session.ExpectedRevision, Is.EqualTo(AuthoringRevision.Compute(changed)));
        });
    }

    [Test]
    public void ReferenceGraph_DeleteRequiresStableSnapshotAndAllRebinds()
    {
        MapAuthoringDocument target = Map("map.target", "target");
        MapAuthoringDocument replacement = Map("map.replacement", "replacement");
        MapAuthoringDocument referring = new(
            "map.referring", 3,
            new[] { new MapAuthoringNode("start", 0, PureRunNodeKind.Battle, "map.target", "Start", 0) },
            Array.Empty<MapAuthoringConnection>());
        MapAuthoringDocument rebound = new(
            "map.referring", 3,
            new[] { new MapAuthoringNode("start", 0, PureRunNodeKind.Battle, "map.replacement", "Start", 0) },
            Array.Empty<MapAuthoringConnection>());
        var graph = new AuthoringReferenceGraph(new[]
        {
            new AuthoringDocumentEnvelope(AuthoringDocumentKind.Map, target),
            new AuthoringDocumentEnvelope(AuthoringDocumentKind.Map, replacement),
            new AuthoringDocumentEnvelope(AuthoringDocumentKind.Map, referring)
        });
        AuthoringReferenceSnapshot snapshot = graph.Capture("map.target");

        AuthoringValidationResult blocked = graph.ValidateDelete("map.target", snapshot.Revision);
        AuthoringValidationResult falseClaim = graph.ValidateDelete(
            "map.target",
            snapshot.Revision,
            new[] { new AuthoringDocumentEnvelope(AuthoringDocumentKind.Map, referring) });
        AuthoringValidationResult stale = graph.ValidateDelete(
            "map.target",
            "sha256:stale",
            new[] { new AuthoringDocumentEnvelope(AuthoringDocumentKind.Map, rebound) });
        AuthoringValidationResult allowed = graph.ValidateDelete(
            "map.target",
            snapshot.Revision,
            new[] { new AuthoringDocumentEnvelope(AuthoringDocumentKind.Map, rebound) });

        Assert.Multiple(() =>
        {
            Assert.That(blocked.Succeeded, Is.False);
            Assert.That(blocked.Diagnostics.Single().Code, Is.EqualTo("authoring.delete_referenced"));
            Assert.That(falseClaim.Succeeded, Is.False);
            Assert.That(falseClaim.Diagnostics.Single().Code, Is.EqualTo("authoring.delete_referenced"));
            Assert.That(stale.Succeeded, Is.False);
            Assert.That(stale.Diagnostics.Single().Code, Is.EqualTo("authoring.reference_revision_conflict"));
            Assert.That(allowed.Succeeded, Is.True);
        });
    }

    [Test]
    public void MapMutation_NodeCrudCleansEdgesAndFencesRevision()
    {
        MapAuthoringDocument source = new(
            "map.test", 3,
            new[]
            {
                new MapAuthoringNode("start", 0, PureRunNodeKind.Battle, "encounter.n1", "Start", 0),
                new MapAuthoringNode("boss", 1, PureRunNodeKind.Boss, "encounter.boss", "Boss", 0)
            },
            new[] { new MapAuthoringConnection("start", "boss") });
        var changeSet = new AuthoringChangeSet(
            "remove-boss", AuthoringDocumentKind.Map, source.ContentId, AuthoringRevision.Compute(source),
            new AuthoringOperation[] { new RemoveMapNodeOperation("boss") });

        MapAuthoringMutationResult result = new MapAuthoringMutationService().Apply(source, changeSet);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Changed, Is.True);
            Assert.That(result.Document.Nodes.Select(value => value.NodeId), Is.EqualTo(new[] { "start" }));
            Assert.That(result.Document.Connections, Is.Empty);
        });
        var stale = new AuthoringChangeSet(
            "stale", AuthoringDocumentKind.Map, source.ContentId, "sha256:stale",
            new AuthoringOperation[] { new UpdateMapNodeOperation(source.Nodes[0] with { Title = "No" }) });
        Assert.That(new MapAuthoringMutationService().Apply(source, stale).Diagnostics.Single().Code,
            Is.EqualTo("map.revision_conflict"));
    }

    [Test]
    public void MapJson_RoundTripsCanonicalAuthoringDocument()
    {
        MapAuthoringDocument source = new(
            "map.test", 3,
            new[]
            {
                new MapAuthoringNode("boss", 1, PureRunNodeKind.Boss, "encounter.boss", "Boss", 0.5f),
                new MapAuthoringNode("start", 0, PureRunNodeKind.Battle, "encounter.n1", "Start", -0.5f)
            },
            new[] { new MapAuthoringConnection("start", "boss") });

        string json = MapAuthoringJson.Serialize(source);
        MapAuthoringDocument restored = MapAuthoringJson.Deserialize(json);

        Assert.Multiple(() =>
        {
            Assert.That(AuthoringRevision.Compute(restored), Is.EqualTo(AuthoringRevision.Compute(source)));
            Assert.That(restored.Nodes.Select(value => value.NodeId), Is.EqualTo(new[] { "boss", "start" }));
            Assert.That(json, Does.Contain("\"layoutVersion\": 3"));
        });
    }

    [Test]
    public void MapValidation_RejectsAmbiguousStartBackwardEdgesAndUnreachableNodes()
    {
        var invalid = new MapAuthoringDocument("map.invalid", 3,
            new[]
            {
                new MapAuthoringNode("start-a", 0, PureRunNodeKind.Battle, "encounter.a", "A", 0),
                new MapAuthoringNode("start-b", 0, PureRunNodeKind.Battle, "encounter.b", "B", 1),
                new MapAuthoringNode("boss", 2, PureRunNodeKind.Boss, "encounter.boss", "Boss", 0),
                new MapAuthoringNode("orphan", 1, PureRunNodeKind.Battle, "encounter.orphan", "Orphan", 0)
            },
            new[] { new MapAuthoringConnection("boss", "start-a") });

        string[] codes = MapAuthoringValidator.Validate(invalid).Select(value => value.Code).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(codes, Does.Contain("map.start_count"));
            Assert.That(codes, Does.Contain("map.non_forward_edge"));
        });

        var valid = new MapAuthoringDocument("map.valid", 3,
            new[]
            {
                new MapAuthoringNode("start", 0, PureRunNodeKind.Battle, "encounter.a", "Start", 0),
                new MapAuthoringNode("boss", 1, PureRunNodeKind.Boss, "encounter.boss", "Boss", 0)
            },
            new[] { new MapAuthoringConnection("start", "boss") });
        Assert.That(MapAuthoringValidator.Validate(valid), Is.Empty);
    }

    [Test]
    public void EventJson_RoundTripsRuntimePayloadWithoutInventingBranchNodes()
    {
        var source = new EventAuthoringDocument("event.test", "event_001", "Chest", "A chest.", new[]
        {
            new EventOptionAuthoring("open", "Open", RunEventAttribute.Strength, 60,
                new EventOutcomeAuthoring(EventOutcomeType.Gold, EventOutcomeTarget.All, 30, null, "Gold."),
                new EventOutcomeAuthoring(EventOutcomeType.Debuff, EventOutcomeTarget.Self, 3, "buff.event-damage-taken-up", "Cursed."))
        }, "frozen/event.json", "sha256:frozen");

        string payload = EventAuthoringJson.SerializePayload(source);
        EventAuthoringDocument restored = EventAuthoringJson.Deserialize(payload);

        Assert.Multiple(() =>
        {
            Assert.That(AuthoringRevision.Compute(restored), Is.EqualTo(AuthoringRevision.Compute(source)));
            Assert.That(restored.Dependencies, Is.EqualTo(new[] { "buff.event-damage-taken-up" }));
            Assert.That(payload, Does.Not.Contain("schemaVersion"));
        });
        Assert.Throws<ArgumentException>(() => new EventAuthoringDocument("event.bad", "bad", "Bad", string.Empty,
            new[] { new EventOptionAuthoring("x", "X", RunEventAttribute.None, 100,
                new EventOutcomeAuthoring(EventOutcomeType.Buff, EventOutcomeTarget.Self, 1, null, string.Empty), null) }));
    }

    [Test]
    public void EventAndTreasureGraphLayout_IsCanonicalAndBackwardCompatible()
    {
        const string legacyEvent = """
            {"contentId":"event.legacy","sourceId":"event_legacy","title":"Legacy","description":"","options":[{"id":"go","text":"Go","attribute":"None","baseSuccessRate":100,"success":{"type":"Nothing","target":"Self","amount":0,"itemId":null,"description":""},"failure":null}],"sourcePath":"","sourceSha256":""}
            """;
        EventAuthoringDocument upgraded = EventAuthoringJson.Deserialize(legacyEvent);
        Assert.That(upgraded.GraphLayout.Nodes, Is.Empty);

        var layout = new AuthoringGraphLayout(new[]
        {
            new AuthoringGraphNodeLayout("option:go", 120.5, 80),
            new AuthoringGraphNodeLayout("start", 10, 20)
        });
        var positioned = new EventAuthoringDocument(upgraded.ContentId, upgraded.SourceId, upgraded.Title,
            upgraded.Description, upgraded.Options, upgraded.SourcePath, upgraded.SourceSha256, layout);
        EventAuthoringDocument restored = EventAuthoringJson.Deserialize(EventAuthoringJson.SerializePayload(positioned));

        var treasureLayout = new AuthoringGraphLayout(new[]
        {
            new AuthoringGraphNodeLayout("treasure:root", 10, 20),
            new AuthoringGraphNodeLayout("treasure:buff", 120.5, 80)
        });
        var treasure = new TreasureAuthoringDocument("treasure.layout", 1, 2,
            new[] { new TreasureEntryAuthoring(TreasureEntryKind.Buff, "buff.test", 1) }, treasureLayout);
        TreasureAuthoringDocument restoredTreasure = TreasureAuthoringJson.Deserialize(TreasureAuthoringJson.Serialize(treasure));
        Assert.Multiple(() =>
        {
            Assert.That(restored.GraphLayout.Nodes.Select(value => value.NodeId), Is.EqualTo(new[] { "option:go", "start" }));
            Assert.That(restoredTreasure.GraphLayout.Nodes.Count, Is.EqualTo(2));
            Assert.That(AuthoringRevision.Compute(positioned), Is.Not.EqualTo(AuthoringRevision.Compute(upgraded)));
            Assert.That(AuthoringRevision.Compute(restored), Is.EqualTo(AuthoringRevision.Compute(positioned)));
            Assert.That(AuthoringRevision.Compute(restoredTreasure), Is.EqualTo(AuthoringRevision.Compute(treasure)));
        });
    }

    [Test]
    public void TreasureDocument_CompilesAndRejectsDuplicateOrNonPositiveEntries()
    {
        var source = new TreasureAuthoringDocument("treasure.test", 2, 5, new[]
        {
            new TreasureEntryAuthoring(TreasureEntryKind.Equipment, "item.equipment.ring", 2),
            new TreasureEntryAuthoring(TreasureEntryKind.Buff, "buff.test", 1)
        });
        Assert.That(source.ToCoreDefinition().Equipment.Single().Weight, Is.EqualTo(2));
        Assert.Throws<ArgumentException>(() => new TreasureAuthoringDocument("treasure.test", 2, 5, new[]
        {
            new TreasureEntryAuthoring(TreasureEntryKind.Buff, "buff.test", 1),
            new TreasureEntryAuthoring(TreasureEntryKind.Buff, "buff.test", 1)
        }));
        Assert.Throws<ArgumentException>(() => new TreasureAuthoringDocument("treasure.test", 2, 5,
            new[] { new TreasureEntryAuthoring(TreasureEntryKind.Buff, "buff.test", 0) }));
    }

    [Test]
    public void PreviewCompiler_UsesCanonicalRevisionAndExactTreasureProbabilities()
    {
        var document = new TreasureAuthoringDocument("treasure.preview", 2, 5, new[]
        {
            new TreasureEntryAuthoring(TreasureEntryKind.Buff, "buff.a", 1),
            new TreasureEntryAuthoring(TreasureEntryKind.Buff, "buff.b", 3)
        });

        AuthoringPreviewEvidence preview = AuthoringPreviewCompiler.Compile(document, 42);
        Assert.Multiple(() =>
        {
            Assert.That(preview.Kind, Is.EqualTo("treasure"));
            Assert.That(preview.Values["revision"], Is.EqualTo(AuthoringRevision.Compute(document)));
            Assert.That(preview.Values["seed"], Is.EqualTo("42"));
            Assert.That(preview.Values["Buff"], Is.EqualTo("buff.a:25%,buff.b:75%"));
        });
    }

    [Test]
    public void EncounterAndLayout_RejectMisalignedBindingsAndOverlappingCells()
    {
        var layout = new BattleLayoutAuthoringDocument("layout.test", new[] { new GridCellAuthoring(1, 1) },
            new[] { new GridCellAuthoring(8, 8) }, new[] { new GridCellAuthoring(5, 5) });
        var encounter = new EncounterAuthoringDocument("encounter.test", layout.ContentId,
            new[] { "unit.enemy" }, new[] { "ai.enemy" }, 1.2f, 1.1f, 2, EncounterClass.Elite);
        Assert.Multiple(() =>
        {
            Assert.That(layout.ToCoreDefinition().BlockedCells, Has.Count.EqualTo(1));
            Assert.That(encounter.ToCoreDefinition().Monsters.Single().AiId.Value, Is.EqualTo("ai.enemy"));
        });
        Assert.Throws<ArgumentException>(() => new EncounterAuthoringDocument("encounter.bad", layout.ContentId,
            new[] { "unit.a", "unit.b" }, new[] { "ai.a" }, 1, 1, 0, EncounterClass.Normal));
        Assert.Throws<ArgumentException>(() => new BattleLayoutAuthoringDocument("layout.bad",
            new[] { new GridCellAuthoring(1, 1) }, new[] { new GridCellAuthoring(1, 1) }, Array.Empty<GridCellAuthoring>()));
        Assert.Throws<InvalidOperationException>(() => EncounterLayoutAuthoringValidator.Validate(
            new EncounterAuthoringDocument("encounter.bad-layout", "layout.other", new[] { "unit.a" }, new[] { "ai.a" }, 1, 1, 0, EncounterClass.Normal), layout));
        Assert.Throws<InvalidOperationException>(() => EncounterLayoutAuthoringValidator.Validate(
            new EncounterAuthoringDocument("encounter.too-many", layout.ContentId, new[] { "unit.a", "unit.b" }, new[] { "ai.a", "ai.b" }, 1, 1, 0, EncounterClass.Normal), layout));
    }

    [Test]
    public void AiAuthoring_UsesTypedNodesAndRejectsDanglingEdges()
    {
        AiAuthoringNode[] nodes =
        [
            new("intent", AiAuthoringNodeKind.Intent, "BasicAttack", true, 2, Array.Empty<AiCurveKeyAuthoring>()),
            new("score", AiAuthoringNodeKind.Score, "TargetHealth", true, 1,
                new[] { new AiCurveKeyAuthoring(0, 0, 0, 0), new AiCurveKeyAuthoring(1, 1, 0, 0) })
        ];
        var source = new AiAuthoringDocument("ai.test", AiArchetype.Charger, new[] { "skill.test" },
            Array.Empty<string>(), 1, 1, 0, 0, nodes, new[] { new AiAuthoringEdge("intent", "score") },
            "sha256:test", 3, 1, 2, .5f);
        Assert.That(source.ToCoreDefinition().DecisionGraph!.Scores.Single().Curve, Has.Count.EqualTo(2));
        Assert.Throws<ArgumentException>(() => new AiAuthoringDocument("ai.bad", AiArchetype.Charger,
            Array.Empty<string>(), Array.Empty<string>(), 1, 1, 0, 0, nodes,
            new[] { new AiAuthoringEdge("intent", "missing") }, "sha256:test", 3, 1, 2, 0));
    }

    [Test]
    public void SkillAuthoring_CoversExecutionProfileAndCanonicalRoundTrip()
    {
        var definition = new SkillDefinition(new ContentId("skill.test"), "test_1", SkillRole.Mage,
            SkillKind.Active, 1, 3, 1, 5, SkillExecutionKind.Fireball, 12, SkillDamageKind.Magical,
            externalDependency: true, executionProfile: new SkillExecutionProfile(2, SummonDefinitionId: new ContentId("unit.summon"), StatusChancePercent: 75,
                BounceRange: 3, BounceCount: 2, PierceAll: true, CorruptionCost: 4));
        var source = new SkillAuthoringDocument(definition, "Fireball", "Test", string.Empty, string.Empty, 0,
            string.Empty, string.Empty);
        SkillAuthoringDocument restored = SkillAuthoringJson.Deserialize(SkillAuthoringJson.Serialize(source));
        Assert.Multiple(() =>
        {
            Assert.That(restored.Definition.ExecutionProfile.StatusChancePercent, Is.EqualTo(75));
            Assert.That(restored.Definition.ExecutionProfile.PierceAll, Is.True);
            Assert.That(restored.Definition.ExecutionProfile.SummonDefinitionId?.Value, Is.EqualTo("unit.summon"));
            Assert.That(restored.Definition.ExecutionProfile.CorruptionCost, Is.EqualTo(4));
            Assert.That(restored.Dependencies, Does.Contain("unit.summon"));
            Assert.That(AuthoringRevision.Compute(restored), Is.EqualTo(AuthoringRevision.Compute(source)));
        });
    }

    [Test]
    public void BatchChangeSet_RejectsDuplicateDocumentIdentity()
    {
        AuthoringDocumentChange change = new(AuthoringDocumentKind.Map, "map.test", "sha256:a", "{}");
        Assert.Throws<ArgumentException>(() => new AuthoringBatchChangeSet("duplicate", new[] { change, change }));
    }

    [Test]
    public void TransactionCoordinator_RollsBackEveryPreparedParticipantInReverseOrder()
    {
        var trace = new List<string>();
        IAuthoringTransactionParticipant[] values =
        [
            new FakeParticipant("a", trace),
            new FakeParticipant("b", trace, failApply: true),
            new FakeParticipant("c", trace)
        ];
        AuthoringTransactionResult result = new AuthoringTransactionCoordinator().Execute(values);
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Applied, Is.EqualTo(new[] { "a" }));
            Assert.That(trace, Is.EqualTo(new[] { "prepare:a", "prepare:b", "prepare:c", "apply:a", "apply:b", "rollback:c", "rollback:b", "rollback:a" }));
        });
    }

    [Test]
    public void PresentationProfile_NormalizesTypedGodotNativeFields()
    {
        var source = new PresentationProfileAuthoringDocument("presentation.test", "SkillPresentationResource",
            new Dictionary<string, PresentationAuthoringValue>
            {
                ["TravelDuration"] = new(PresentationAuthoringValueKind.Number, "0.28"),
                ["PrimaryColor"] = new(PresentationAuthoringValueKind.Color, "1,0.2,0.1,1"),
                ["MaximumGhosts"] = new(PresentationAuthoringValueKind.Integer, "3")
            });
        PresentationProfileAuthoringDocument restored = PresentationProfileAuthoringJson.Deserialize(PresentationProfileAuthoringJson.Serialize(source));
        Assert.That(AuthoringRevision.Compute(restored), Is.EqualTo(AuthoringRevision.Compute(source)));
    }

    [Test]
    public void PresentationGraph_DefaultRoundTripsAndRejectsCyclesOrUnknownLeaves()
    {
        var profile = new PresentationProfileAuthoringDocument("presentation.test", "SkillPresentationResource",
            new Dictionary<string, PresentationAuthoringValue>
            {
                ["TravelDuration"] = new(PresentationAuthoringValueKind.Number, "0.2"),
                ["AuthoringGraphJsonValue"] = new(PresentationAuthoringValueKind.String, string.Empty)
            });
        PresentationGraphAuthoringDocument graph = PresentationGraphAuthoringDocument.CreateDefault(profile);
        PresentationGraphAuthoringDocument restored = PresentationGraphAuthoringJson.Deserialize(PresentationGraphAuthoringJson.Serialize(graph));
        Assert.DoesNotThrow(() => restored.Validate(profile.Properties.Keys.Where(value => value != "AuthoringGraphJsonValue")));
        Assert.Throws<ArgumentException>(() => new PresentationGraphAuthoringDocument(
            new[] { new PresentationGraphNode("root", PresentationGraphNodeKind.Root, string.Empty, 0, 0), new PresentationGraphNode("leaf", PresentationGraphNodeKind.Property, "TravelDuration", 1, 1) },
            new[] { new PresentationGraphEdge("root", "leaf"), new PresentationGraphEdge("leaf", "root") }));
        Assert.Throws<ArgumentException>(() => new PresentationGraphAuthoringDocument(
            new[] { new PresentationGraphNode("root", PresentationGraphNodeKind.Root, string.Empty, 0, 0), new PresentationGraphNode("leaf", PresentationGraphNodeKind.Property, "Unknown", 1, 1) },
            new[] { new PresentationGraphEdge("root", "leaf") }).Validate(new[] { "TravelDuration" }));
        Assert.Throws<ArgumentException>(() => new PresentationGraphAuthoringDocument(
            new[] { new PresentationGraphNode("root", PresentationGraphNodeKind.Root, string.Empty, 0, 0), new PresentationGraphNode("delay", PresentationGraphNodeKind.Delay, string.Empty, 1, 1) },
            new[] { new PresentationGraphEdge("root", "delay") }).ValidateRuntimeCompatibility());
        Assert.DoesNotThrow(() => new PresentationGraphAuthoringDocument(
            new[] { new PresentationGraphNode("root", PresentationGraphNodeKind.Root, string.Empty, 0, 0), new PresentationGraphNode("delay", PresentationGraphNodeKind.Delay, string.Empty, 1, 1, Enabled: false) },
            new[] { new PresentationGraphEdge("root", "delay") }).ValidateRuntimeCompatibility());
    }

    private sealed class FakeParticipant(string identity, List<string> trace, bool failApply = false) : IAuthoringTransactionParticipant
    {
        public string Identity => identity;
        public void Prepare() => trace.Add("prepare:" + identity);
        public void Apply() { trace.Add("apply:" + identity); if (failApply) throw new InvalidOperationException("fault"); }
        public void Rollback() => trace.Add("rollback:" + identity);
    }

    private static MapAuthoringDocument Map(string contentId, string title) => new(
        contentId, 3,
        new[] { new MapAuthoringNode("start", 0, PureRunNodeKind.Battle, "encounter.n1", title, 0) },
        Array.Empty<MapAuthoringConnection>());

    private static SkillDefinition Skill(string id, SkillExecutionKind execution, int manaCost, int damage) =>
        new(new ContentId(id), id, SkillRole.Any, SkillKind.Active, 1, manaCost, 1, 4, execution,
            damage, SkillDamageKind.Physical);

    private static BattleState BattleStateForPreview(UnitInstanceId actorId, UnitInstanceId enemyId)
    {
        Dictionary<GridPoint, CellState> cells = Enumerable.Range(0, BoardSpec.Width)
            .SelectMany(x => Enumerable.Range(0, BoardSpec.Height)
                .Select(y => new KeyValuePair<GridPoint, CellState>(new GridPoint(x, y), new CellState())))
            .ToDictionary();
        var actor = new BattleUnitState(new UnitState(actorId, new ContentId("unit.preview.actor"),
            new GridPoint(1, 1), 3, 10, 0, 0), 20, 20, maxMana: 20, currentMana: 20);
        var enemy = new BattleUnitState(new UnitState(enemyId, new ContentId("unit.preview.enemy"),
            new GridPoint(2, 1), 3, 8, 1, 0), 20, 20);
        return new BattleState(new BoardSnapshot(cells), [actor, enemy], [actorId, enemyId], randomState: 42);
    }
}
