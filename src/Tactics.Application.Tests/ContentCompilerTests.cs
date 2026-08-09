using NUnit.Framework;
using Tactics.Application.Content;
using Tactics.Core.Content;

namespace Tactics.Application.Tests;

public sealed class ContentCompilerTests
{
    [Test]
    public void Compile_ProducesEngineNeutralSnapshotForValidDrafts()
    {
        var presentationId = new ContentId("presentation.poison-spear.lv1");
        var result = new ContentCompiler().Compile(new[]
        {
            new ContentDraft(presentationId, "presentation", 1),
            new ContentDraft(
                new ContentId("skill.poison-spear.lv1"),
                "skill",
                1,
                new[] { presentationId },
                new Dictionary<string, string> { ["damage"] = "8" })
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.Snapshot!.Entries.Count, Is.EqualTo(2));
            Assert.That(result.Snapshot.Get(new ContentId("skill.poison-spear.lv1")).Properties["damage"], Is.EqualTo("8"));
        });
    }

    [Test]
    public void Compile_RejectsDuplicateAndMissingReferences()
    {
        var duplicate = new ContentId("skill.duplicate");
        var result = new ContentCompiler().Compile(new[]
        {
            new ContentDraft(duplicate, "skill", 1, new[] { new ContentId("presentation.missing") }),
            new ContentDraft(duplicate, "skill", 1)
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Snapshot, Is.Null);
            Assert.That(result.Diagnostics.Select(item => item.Code), Does.Contain("content.duplicate_id"));
            Assert.That(result.Diagnostics.Select(item => item.Code), Does.Contain("content.missing_reference"));
        });
    }

    [Test]
    public void Compile_ReportsInvalidSchemaAndResourceType()
    {
        var result = new ContentCompiler().Compile(new[]
        {
            new ContentDraft(new ContentId("content.invalid"), "", 0)
        });

        Assert.That(result.Diagnostics.Select(item => item.Code), Is.EquivalentTo(new[]
        {
            "content.invalid_schema",
            "content.empty_resource_type"
        }));
    }

    [Test]
    public void Compile_RejectsUnknownResourceTypeAndUnsupportedSchema()
    {
        var result = new ContentCompiler().Compile(new[]
        {
            new ContentDraft(new ContentId("content.unknown"), "unknown", 1),
            new ContentDraft(new ContentId("skill.future"), "skill", 2)
        });

        Assert.That(result.Diagnostics.Select(item => item.Code), Is.EquivalentTo(new[]
        {
            "content.unknown_resource_type",
            "content.unsupported_schema"
        }));
    }

    [Test]
    public void SchemaCatalog_RejectsInvalidAndDuplicateDefinitions()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                () => new ContentSchemaCatalog(new[] { new ContentSchemaDefinition("skill", 2, 1) }),
                Throws.ArgumentException);
            Assert.That(
                () => new ContentSchemaCatalog(new[]
                {
                    new ContentSchemaDefinition("skill", 1, 1),
                    new ContentSchemaDefinition("skill", 1, 1)
                }),
                Throws.ArgumentException);
        });
    }

    [Test]
    public void Compile_AcceptsRealPoisonSpearTargetDependencyGraph()
    {
        var poison = new ContentId("buff.poison");
        var projectile = new ContentId("projectile.poison-spear");
        var impact = new ContentId("impact.poison-spear");
        var presentation = new ContentId("presentation.poison-spear.lv1");
        var result = new ContentCompiler().Compile(new[]
        {
            new ContentDraft(poison, "buff", 1),
            new ContentDraft(projectile, "packed-scene", 1),
            new ContentDraft(impact, "packed-scene", 1),
            new ContentDraft(presentation, "presentation", 1, new[] { projectile, impact }),
            new ContentDraft(
                new ContentId("skill.poison-spear.lv1"),
                "skill",
                1,
                new[] { poison, presentation }),
            new ContentDraft(new ContentId("encounter.poison-spear.10x10"), "encounter", 1)
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.Snapshot!.Entries.Count, Is.EqualTo(6));
        });
    }
}
