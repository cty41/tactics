using NUnit.Framework;
using Tactics.Application.Runs;
using Tactics.Core.Content;
using Tactics.Core.Runs;

namespace Tactics.Application.Tests;

public sealed class RunSaveDocumentV5Tests
{
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
}
