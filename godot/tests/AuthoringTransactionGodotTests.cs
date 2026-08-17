using GdUnit4;
using Godot;
using Tactics.Application.Authoring;
using Tactics.Godot.Adapter.Editor;
using Tactics.Godot.Adapter.Runtime;
using static GdUnit4.Assertions;

namespace Tactics.Godot.Tests;

[TestSuite]
public class AuthoringTransactionGodotTests
{
    [TestCase]
    [RequireGodotRuntime]
    public void BatchSaveRestoresEveryFileWhenSecondSaveFails()
    {
        string root = $"user://authoring-rollback-{Guid.NewGuid():N}";
        string firstPath = root + "-first.tres";
        string secondPath = root + "-second.tres";
        SaveEntry(firstPath, "before.first");
        SaveEntry(secondPath, "before.second");

        try
        {
            WorkbenchResourceBatchSaveService.SaveWithRollback(
                new[]
                {
                    Request(firstPath, "after.first"),
                    Request(secondPath, "after.second")
                },
                (checkpoint, index) =>
                {
                    if (checkpoint == WorkbenchResourceSaveCheckpoint.Saved && index == 1)
                        throw new InjectedSaveFailureException();
                });
            AssertThat(false).IsTrue();
        }
        catch (InjectedSaveFailureException)
        {
            AssertThat(LoadEntry(firstPath).ContentIdValue).IsEqual("before.first");
            AssertThat(LoadEntry(secondPath).ContentIdValue).IsEqual("before.second");
        }
        finally
        {
            Delete(firstPath);
            Delete(secondPath);
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void BatchSaveRestoresResourceAndDeletedFileWhenMutationFails()
    {
        string root = $"user://authoring-mutation-rollback-{Guid.NewGuid():N}";
        string resourcePath = root + "-resource.tres";
        string deletedPath = root + "-deleted.tres";
        SaveEntry(resourcePath, "before.resource");
        SaveEntry(deletedPath, "before.deleted");

        try
        {
            WorkbenchResourceBatchSaveService.SaveWithRollback(
                [Request(resourcePath, "after.resource")],
                [new WorkbenchFileMutationRequest(deletedPath, null)],
                (checkpoint, _) =>
                {
                    if (checkpoint == WorkbenchResourceSaveCheckpoint.FileMutated)
                        throw new InjectedSaveFailureException();
                });
            AssertThat(false).IsTrue();
        }
        catch (InjectedSaveFailureException)
        {
            AssertThat(LoadEntry(resourcePath).ContentIdValue).IsEqual("before.resource");
            AssertThat(LoadEntry(deletedPath).ContentIdValue).IsEqual("before.deleted");
        }
        finally
        {
            Delete(resourcePath);
            Delete(deletedPath);
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DuplicateRollbackRestoresCatalogLedgerUidAndNewResource()
    {
        const string contentId = "treasure.workbench.rollback-test";
        string path = AuthoringResourceLifecycleService.AuthoredRoot + "/treasure/treasure-workbench-rollback-test.tres";
        string ledgerAbsolute = ProjectSettings.GlobalizePath(AuthoringResourceLifecycleService.LedgerPath);
        byte[]? ledgerBefore = File.Exists(ledgerAbsolute) ? File.ReadAllBytes(ledgerAbsolute) : null;
        GodotResourceCatalog catalogBefore = new TacticsAuthoringEditorService().LoadCatalog();

        try
        {
            var batch = new AuthoringBatchChangeSet("test-rollback", Array.Empty<AuthoringDocumentChange>(),
                [new AuthoringAssetChange(AuthoringAssetChangeKind.Duplicate, contentId,
                    "treasure.pure-run.standard-v1", "treasure")]);
            _ = new AuthoringResourceLifecycleService().ApplyBatch(batch, (checkpoint, index) =>
                {
                    if (checkpoint == WorkbenchResourceSaveCheckpoint.Saved && index == 1)
                        throw new InjectedSaveFailureException();
                });
            AssertThat(false).IsTrue();
        }
        catch (InjectedSaveFailureException)
        {
            GodotResourceCatalog catalogAfter = new TacticsAuthoringEditorService().LoadCatalog();
            byte[]? ledgerAfter = File.Exists(ledgerAbsolute) ? File.ReadAllBytes(ledgerAbsolute) : null;
            AssertThat(catalogAfter.Entries.Any(value => value.ContentIdValue == contentId)).IsFalse();
            AssertThat(File.Exists(ProjectSettings.GlobalizePath(path))).IsFalse();
            AssertThat(ledgerAfter ?? Array.Empty<byte>()).IsEqual(ledgerBefore ?? Array.Empty<byte>());
            AssertThat(catalogAfter.Entries.Length).IsEqual(catalogBefore.Entries.Length);
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DeleteRejectsFormalReceiptOwnedResource()
    {
        var lifecycle = new AuthoringResourceLifecycleService();
        AuthoringReferenceSnapshot references = lifecycle.CaptureReferences("treasure.pure-run.standard-v1");
        try
        {
            _ = new TacticsAuthoringEditorService().ApplyBatch(new AuthoringBatchChangeSet("protected-delete-test",
                Array.Empty<AuthoringDocumentChange>(), [new AuthoringAssetChange(AuthoringAssetChangeKind.Delete,
                    "treasure.pure-run.standard-v1", ResourceType: "treasure", ExpectedReferenceRevision: references.Revision)]));
            AssertThat(false).IsTrue();
        }
        catch (InvalidOperationException exception)
        {
            AssertThat(exception.Message).Contains("protected");
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void LifecycleBatchUndoRedoRestoresTheSameUidAndCatalogIdentity()
    {
        string contentId = "treasure.workbench.transaction-" + Guid.NewGuid().ToString("N");
        string changeId = "lifecycle-" + Guid.NewGuid().ToString("N");
        var editor = new TacticsAuthoringEditorService();
        var batch = new AuthoringBatchChangeSet(changeId, Array.Empty<AuthoringDocumentChange>(),
            [new AuthoringAssetChange(AuthoringAssetChangeKind.Duplicate, contentId,
                "treasure.pure-run.standard-v1", "treasure")]);
        string? uid = null;
        try
        {
            StoredAuthoringDocument created = editor.ApplyBatch(batch).Single();
            uid = created.Entry.ResourceUidValue;
            editor.UndoLifecycleBatch(changeId);
            AssertThat(editor.LoadCatalog().Entries.Any(value => value.ContentIdValue == contentId)).IsFalse();

            StoredAuthoringDocument redone = editor.ApplyBatch(batch).Single();
            AssertThat(redone.Entry.ResourceUidValue).IsEqual(uid);
            AssertThat(editor.LoadCatalog().Entries.Count(value => value.ContentIdValue == contentId)).IsEqual(1);
        }
        finally
        {
            if (uid is not null && editor.LoadCatalog().Entries.Any(value => value.ContentIdValue == contentId))
                editor.UndoLifecycleBatch(changeId);
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void LifecycleValidationRunsProspectiveChecksWithoutWritingState()
    {
        string contentId = "treasure.workbench.validate-" + Guid.NewGuid().ToString("N");
        string path = AuthoringResourceLifecycleService.AuthoredRoot + "/treasure/" + contentId + ".tres";
        string ledgerAbsolute = ProjectSettings.GlobalizePath(AuthoringResourceLifecycleService.LedgerPath);
        byte[]? ledgerBefore = File.Exists(ledgerAbsolute) ? File.ReadAllBytes(ledgerAbsolute) : null;
        var editor = new TacticsAuthoringEditorService();
        int catalogCount = editor.LoadCatalog().Entries.Length;
        var batch = new AuthoringBatchChangeSet("validate-only", Array.Empty<AuthoringDocumentChange>(),
            [new AuthoringAssetChange(AuthoringAssetChangeKind.Duplicate, contentId,
                "treasure.pure-run.standard-v1", "treasure", path)]);

        editor.ValidateBatch(batch);

        byte[]? ledgerAfter = File.Exists(ledgerAbsolute) ? File.ReadAllBytes(ledgerAbsolute) : null;
        AssertThat(editor.LoadCatalog().Entries.Length).IsEqual(catalogCount);
        AssertThat(File.Exists(ProjectSettings.GlobalizePath(path))).IsFalse();
        AssertThat(ledgerAfter ?? Array.Empty<byte>()).IsEqual(ledgerBefore ?? Array.Empty<byte>());
    }

    [TestCase]
    [RequireGodotRuntime]
    public void LifecycleValidationRejectsAnAuthoredPathThatEscapesItsRoot()
    {
        string contentId = "treasure.workbench.escape-" + Guid.NewGuid().ToString("N");
        var batch = new AuthoringBatchChangeSet("escape-path", Array.Empty<AuthoringDocumentChange>(),
            [new AuthoringAssetChange(AuthoringAssetChangeKind.Duplicate, contentId,
                "treasure.pure-run.standard-v1", "treasure",
                AuthoringResourceLifecycleService.AuthoredRoot + "/../escape-test.tres")]);
        try
        {
            new TacticsAuthoringEditorService().ValidateBatch(batch);
            AssertThat(false).IsTrue();
        }
        catch (InvalidOperationException exception)
        {
            AssertThat(exception.Message).Contains("escapes");
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void BatchSaveRejectsRawByteWritesForTypedGodotResources()
    {
        try
        {
            WorkbenchResourceBatchSaveService.SaveWithRollback(
                Array.Empty<WorkbenchResourceSaveRequest>(),
                [new WorkbenchFileMutationRequest("user://authoring-raw-write.tres", [1, 2, 3])]);
            AssertThat(false).IsTrue();
        }
        catch (ArgumentException exception)
        {
            AssertThat(exception.Message).Contains("ResourceSaver");
        }
    }

    private static WorkbenchResourceSaveRequest Request(string path, string contentId) =>
        new(new GodotResourceEntry
        {
            ContentIdValue = contentId,
            ResourceTypeIdValue = "test",
            ResourceUidValue = "uid://test",
            DiagnosticPathValue = path,
            SchemaVersion = 1
        }, path, resource =>
        {
            if (resource is not GodotResourceEntry entry || entry.ContentIdValue != contentId)
                throw new InvalidOperationException("Reloaded authoring test Resource changed.");
        });

    private static void SaveEntry(string path, string contentId)
    {
        Error error = ResourceSaver.Save((GodotResourceEntry)Request(path, contentId).Resource, path);
        if (error != Error.Ok) throw new InvalidOperationException($"Could not create transaction fixture: {error}.");
    }

    private static GodotResourceEntry LoadEntry(string path) =>
        ResourceLoader.Load<GodotResourceEntry>(path, string.Empty, ResourceLoader.CacheMode.Ignore)
        ?? throw new InvalidOperationException($"Could not reload transaction fixture '{path}'.");

    private static void Delete(string path)
    {
        string absolute = ProjectSettings.GlobalizePath(path);
        if (File.Exists(absolute)) File.Delete(absolute);
    }

    private sealed class InjectedSaveFailureException : Exception;
}
