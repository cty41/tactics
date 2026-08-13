using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NUnit.Framework;
using Tactics.Application.Runs;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Application.Tests;

public sealed class RunSaveDocumentV5Tests
{
    [Test]
    public void HistoricalPendingBattleAlias_IsDecodedAndResumedWithCanonicalContentId()
    {
        var attributes = new UnitAttributes(5, 6, 5, 5, 5, 5);
        RunCharacterState Character(string characterId, string unitId, string skillId) => new(
            characterId,
            new ContentId(unitId),
            1,
            attributes,
            20,
            20,
            5,
            15,
            false,
            [new ContentId(skillId)]);
        RunCharacterState[] party =
        [
            Character("pure_run_mage", "unit.pure-run.mage", "skill.mage.fireball.lv1"),
            Character("pure_run_necromancer", "unit.pure-run.necromancer", "skill.necromancer.bone-spear.lv1"),
            Character("pure_run_amazon", "unit.pure-run.amazon", "skill.poison-spear.lv1")
        ];
        var checkpoint = new RunEncounterCheckpoint(
            new ContentId("encounter.pure-run.n1"), 0, 1, party, [], []);
        var run = new PureRunState(
            "run-alias-repair",
            12,
            1,
            PureRunPhase.PendingBattle,
            0,
            new ContentId("encounter.pure-run.n1"),
            party,
            pendingProgression:
            [
                new PendingProgression("battle:n1:progression", "n1", "pure_run_amazon",
                    SelectedSkillContentId: new ContentId("skill.poison-spear.lv1"))
            ],
            checkpoint: checkpoint);

        string historicalWire = HistoricalAliasWire(new PureRunSaveSnapshot(1, run, null));
        RunSaveDecodeResultV5 decoded = RunSaveDocumentV5.Decode(historicalWire);
        var store = new MemoryRunStore { Snapshot = decoded.Snapshot };
        RunSessionResult resumed = new PureRunSessionService(Definition(), store).ResumeRun();

        RunCharacterState repaired = decoded.Snapshot!.ActiveRun!.Party.Single(value =>
            value.CharacterId == "pure_run_amazon");
        RunCharacterState checkpointAmazon = decoded.Snapshot.ActiveRun.Checkpoint!.Party.Single(value =>
            value.CharacterId == "pure_run_amazon");
        Assert.Multiple(() =>
        {
            Assert.That(decoded.Succeeded, Is.True);
            Assert.That(historicalWire, Does.Contain("skill.amazon.poison-spear.lv1"));
            Assert.That(repaired.LearnedSkills,
                Is.EqualTo(new[] { new ContentId("skill.poison-spear.lv1") }));
            Assert.That(repaired.LearnedSkillStates.Single().DefinitionId,
                Is.EqualTo(new ContentId("skill.poison-spear.lv1")));
            Assert.That(repaired.LearnedSkillStates.Single().BranchId,
                Is.EqualTo("amazon.poison-spear"));
            Assert.That(checkpointAmazon.LearnedSkills.Single(),
                Is.EqualTo(new ContentId("skill.poison-spear.lv1")));
            Assert.That(decoded.Snapshot.ActiveRun.PendingProgression.Single().SelectedSkillContentId, Is.Null);
            Assert.That(decoded.Snapshot.ActiveRun.PendingProgression.Single().ProposedAttributes, Is.Null);
            Assert.That(resumed.Succeeded, Is.True);
            Assert.That(resumed.EncounterRequest!.Party.Single(value => value.CharacterId == "pure_run_amazon")
                .LearnedSkills.Single(), Is.EqualTo(new ContentId("skill.poison-spear.lv1")));
        });
    }

    [Test]
    public void PendingSetup_RoundTripsAndV4MigratesDeterministically()
    {
        var setup = new PendingRunSetup(12, 2,
        [
            new PendingRunStartingSkillChoice("mage", new ContentId("skill.mage.ice-bolt.lv1")),
            new PendingRunStartingSkillChoice("necromancer", new ContentId("skill.necromancer.bone-spear.lv1"))
        ]);
        var snapshot = new PureRunSaveSnapshot(1, null, null, setup);

        string encoded = RunSaveDocumentV5.Encode(snapshot);
        RunSaveDecodeResultV5 decoded = RunSaveDocumentV5.Decode(encoded);
        string v4 = RunSaveDocumentV4.Encode(new PureRunSaveSnapshot(0, null, null));
        RunSaveDecodeResultV5 migrated = RunSaveDocumentV5.Decode(v4);

        Assert.Multiple(() =>
        {
            Assert.That(decoded.Succeeded, Is.True);
            Assert.That(decoded.Snapshot!.PendingRunSetup!.Seed, Is.EqualTo(setup.Seed));
            Assert.That(decoded.Snapshot.PendingRunSetup.CurrentCharacterIndex, Is.EqualTo(2));
            Assert.That(decoded.Snapshot.PendingRunSetup.Choices, Is.EqualTo(setup.Choices));
            Assert.That(encoded, Does.Contain("\"schemaVersion\": 5"));
            Assert.That(migrated.Succeeded, Is.True);
            Assert.That(migrated.MigratedFromSchema, Is.EqualTo(4));
            Assert.That(RunSaveDocumentV5.Encode(migrated.Snapshot!), Is.EqualTo(RunSaveDocumentV5.Encode(migrated.Snapshot!)));
        });
    }

    private static string HistoricalAliasWire(PureRunSaveSnapshot snapshot)
    {
        string canonical = RunSaveDocumentV5.Encode(snapshot);
        string historical = canonical.Replace(
            "\"skill.poison-spear.lv1\"",
            "\"skill.amazon.poison-spear.lv1\"",
            StringComparison.Ordinal);
        JsonNode root = JsonNode.Parse(historical) ?? throw new InvalidOperationException("Historical wire is invalid.");
        string payload = root["payload"]!.ToJsonString();
        root["payloadSha256"] = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static PureRunDefinition Definition() => new(
        new ContentId("run.pure-run.three-encounter-v1"),
        [
            new ContentId("encounter.pure-run.n1"),
            new ContentId("encounter.pure-run.n2"),
            new ContentId("encounter.pure-run.n3")
        ],
        [
            new PureRunPartyTemplate("pure_run_mage", new ContentId("unit.pure-run.mage"),
                new ContentId("skill.mage.fireball.lv1"), new UnitAttributes(5, 5, 5, 6, 5, 5)),
            new PureRunPartyTemplate("pure_run_necromancer", new ContentId("unit.pure-run.necromancer"),
                new ContentId("skill.necromancer.summon-skeleton.lv1"), new UnitAttributes(5, 5, 5, 5, 6, 5)),
            new PureRunPartyTemplate("pure_run_amazon", new ContentId("unit.pure-run.amazon"),
                new ContentId("skill.poison-spear.lv1"), new UnitAttributes(5, 6, 5, 5, 5, 5))
        ]);

    private sealed class MemoryRunStore : IRunSaveStore
    {
        public PureRunSaveSnapshot? Snapshot { get; init; }
        public RunStoreResult Load() => new(true, null, Snapshot);
        public RunStoreResult Save(PureRunSaveSnapshot snapshot, long expectedRevision) =>
            new(false, "save.unexpected_write", Snapshot);
    }
}
