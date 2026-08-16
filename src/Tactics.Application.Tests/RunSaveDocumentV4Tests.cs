using NUnit.Framework;
using Tactics.Application.Runs;

namespace Tactics.Application.Tests;

public sealed class RunSaveDocumentV4Tests
{
    [Test]
    public void V4IsCanonicalAndMigratesV3()
    {
        var snapshot = new PureRunSaveSnapshot(0, null, null);
        string first = RunSaveDocumentV4.Encode(snapshot);
        Assert.That(RunSaveDocumentV4.Encode(snapshot), Is.EqualTo(first));
        RunSaveDecodeResultV4 decoded = RunSaveDocumentV4.Decode(first);
        Assert.That(decoded.Succeeded, Is.True);
        string legacy = RunSaveDocumentV3.Encode(snapshot);
        RunSaveDecodeResultV4 migrated = RunSaveDocumentV4.Decode(legacy);
        Assert.Multiple(() =>
        {
            Assert.That(migrated.Succeeded, Is.True);
            Assert.That(migrated.MigratedFromSchema, Is.EqualTo(3));
            Assert.That(RunSaveDocumentV4.Encode(migrated.Snapshot!), Does.Contain("\"schemaVersion\": 4"));
        });
    }

    [Test]
    public void FutureSchemaIsRejected()
    {
        string future = RunSaveDocumentV4.Encode(new PureRunSaveSnapshot(0, null, null))
            .Replace("\"schemaVersion\": 4", "\"schemaVersion\": 5", StringComparison.Ordinal);
        Assert.That(RunSaveDocumentV4.Decode(future).ErrorCode, Is.EqualTo("save.unsupported_schema"));
    }
}
