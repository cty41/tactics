using NUnit.Framework;
using Tactics.Application.Runs;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Application.Tests;

public sealed class RunSaveDocumentV1Tests
{
    [Test]
    public void Encode_IsByteStableAndRoundTrips()
    {
        PureRunSaveSnapshot snapshot = Snapshot();
        string first = RunSaveDocumentV1.Encode(snapshot);
        string second = RunSaveDocumentV1.Encode(snapshot);
        RunSaveDecodeResult decoded = RunSaveDocumentV1.Decode(first);

        Assert.Multiple(() =>
        {
            Assert.That(second, Is.EqualTo(first));
            Assert.That(decoded.Succeeded, Is.True, decoded.ErrorCode);
            Assert.That(decoded.Envelope!.Revision, Is.EqualTo(1));
            Assert.That(decoded.Envelope.Payload.ActiveRun!.EncounterContentId.Value,
                Is.EqualTo("encounter.pure-run.n1"));
            Assert.That(decoded.Envelope.Payload.ActiveRun.Party[0].Attributes,
                Is.EqualTo(new UnitAttributes(5, 5, 5, 6, 5, 5)));
        });
    }

    [Test]
    public void Decode_RejectsPayloadTampering()
    {
        string encoded = RunSaveDocumentV1.Encode(Snapshot());
        string tampered = encoded.Replace("encounter.pure-run.n1", "encounter.pure-run.n2", StringComparison.Ordinal);
        Assert.That(RunSaveDocumentV1.Decode(tampered).ErrorCode, Is.EqualTo("save.payload_hash_mismatch"));
    }

    [Test]
    public void Encode_DoesNotContainWallClockTimestamp()
    {
        string encoded = RunSaveDocumentV1.Encode(Snapshot());
        Assert.That(encoded, Does.Not.Contain("writtenAt"));
        Assert.That(encoded, Does.Not.Contain("timestamp"));
    }

    private static PureRunSaveSnapshot Snapshot()
    {
        var attributes = new UnitAttributes(5, 5, 5, 6, 5, 5);
        RunCharacterState Member(string id, string unit) => new(
            id, new ContentId(unit), 1, attributes,
            20, 20, 5, 15, false, new[] { new ContentId("skill.mage.fireball.lv1") });
        var party = new[] {
            Member("mage", "unit.pure-run.mage"),
            Member("necro", "unit.pure-run.necromancer"),
            Member("amazon", "unit.pure-run.amazon") };
        var run = new PureRunState(
            "run-test", 42, 1, PureRunPhase.Ready, 0,
            new ContentId("encounter.pure-run.n1"), party);
        return new PureRunSaveSnapshot(1, run, null);
    }
}
