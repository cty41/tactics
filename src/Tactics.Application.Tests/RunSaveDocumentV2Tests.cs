using NUnit.Framework;
using Tactics.Application.Runs;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Application.Tests;

public sealed class RunSaveDocumentV2Tests
{
    [Test]
    public void V2RoundTripPreservesExplicitLearnedSkillLevels()
    {
        PureRunSaveSnapshot snapshot = Snapshot();
        string encoded = RunSaveDocumentV2.Encode(snapshot);
        RunSaveDecodeResultV2 decoded = RunSaveDocumentV2.Decode(encoded);
        Assert.Multiple(() =>
        {
            Assert.That(decoded.Succeeded, Is.True);
            Assert.That(decoded.MigratedFromV1, Is.False);
            Assert.That(decoded.Snapshot!.ActiveRun!.Party[0].LearnedSkillStates.Single().Level, Is.EqualTo(2));
        });
    }

    [Test]
    public void V1ReadMapsLegacySkillIdsAndCanBeWrittenAsV2()
    {
        string legacy = RunSaveDocumentV1.Encode(Snapshot());
        RunSaveDecodeResultV2 decoded = RunSaveDocumentV2.Decode(legacy);
        string upgraded = RunSaveDocumentV2.Encode(decoded.Snapshot!);
        Assert.Multiple(() =>
        {
            Assert.That(decoded.Succeeded, Is.True);
            Assert.That(decoded.MigratedFromV1, Is.True);
            Assert.That(upgraded, Does.Contain("\"schemaVersion\": 2"));
        });
    }

    private static PureRunSaveSnapshot Snapshot()
    {
        var attributes = new UnitAttributes(3, 3, 3, 5, 4, 2);
        var character = new RunCharacterState("mage", new ContentId("unit.pure-run.mage"), 2, attributes,
            20, 20, 12, 12, false, new[] { new ContentId("skill.mage.fireball.lv2") },
            learnedSkillStates: new[] { new RunLearnedSkillState("mage.fireball", 2, new ContentId("skill.mage.fireball.lv2")) });
        RunCharacterState Clone(string id, string unit) => new(id, new ContentId(unit), 1, attributes, 20, 20, 12, 12, false,
            new[] { new ContentId(id == "amazon" ? "skill.amazon.thrust.lv1" : "skill.necromancer.summon-skeleton.lv1") });
        var run = new PureRunState("run-v2", 5, 4, PureRunPhase.Ready, 1, new ContentId("encounter.pure-run.n2"),
            new[] { character, Clone("necromancer", "unit.pure-run.necromancer"), Clone("amazon", "unit.pure-run.amazon") });
        return new PureRunSaveSnapshot(4, run, null);
    }
}
