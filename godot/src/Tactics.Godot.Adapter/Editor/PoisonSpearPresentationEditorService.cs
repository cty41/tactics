#if TOOLS
using Godot;
using Tactics.Application.Presentation;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

/// <summary>
/// Maps the pure Application presentation mutation contract onto one Godot Resource.
/// </summary>
public static class PoisonSpearPresentationEditorService
{
    private const string RuntimeRootNodeId = "__poison_spear.runtime";

    public static PresentationGraphDocument Read(Resource presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        string[] nodeIds = presentation.Get("AuthoringNodeIds").AsStringArray();
        string[] nodeTypes = presentation.Get("AuthoringNodeTypes").AsStringArray();
        string[] nodeKinds = presentation.Get("AuthoringNodeKinds").AsStringArray();
        string[] nodeCues = presentation.Get("AuthoringNodeCues").AsStringArray();
        int[] nodeEnabled = presentation.Get("AuthoringNodeEnabled").AsInt32Array();
        Vector2[] nodePositions = presentation.Get("AuthoringNodePositions").AsVector2Array();
        string[] edgeIds = presentation.Get("EdgeIds").AsStringArray();
        string[] edgeSources = presentation.Get("EdgeSources").AsStringArray();
        string[] edgeTargets = presentation.Get("EdgeTargets").AsStringArray();
        if (nodeIds.Length != nodeTypes.Length || nodeIds.Length != nodeKinds.Length ||
            nodeIds.Length != nodeCues.Length || nodeIds.Length != nodeEnabled.Length ||
            nodeIds.Length != nodePositions.Length)
        {
            throw new InvalidOperationException("Presentation authoring node arrays must have equal lengths.");
        }
        if (edgeIds.Length != edgeSources.Length || edgeIds.Length != edgeTargets.Length)
            throw new InvalidOperationException("Presentation authoring edge arrays must have equal lengths.");

        return new PresentationGraphDocument(
            presentation.Get("SchemaVersion").AsInt32(),
            nodeIds.Select((nodeId, index) => new PresentationNodeDocument(
                nodeId,
                nodeTypes[index],
                nodeKinds[index],
                nodeCues[index],
                nodeEnabled[index] != 0,
                new PresentationNodePosition(nodePositions[index].X, nodePositions[index].Y))),
            edgeIds.Select((edgeId, index) => new PresentationEdgeDocument(
                edgeId,
                edgeSources[index],
                edgeTargets[index])));
    }

    public static string SynchronizeRevision(Resource presentation)
    {
        PresentationGraphDocument document = Read(presentation);
        presentation.Set("Revision", document.Revision);
        return document.Revision;
    }

    public static PresentationGraphMutationResult Apply(
        Resource presentation,
        PresentationGraphChangeSet changeSet)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(changeSet);
        PresentationGraphDocument source = Read(presentation);
        string storedRevision = presentation.Get("Revision").AsString();
        if (!string.Equals(storedRevision, source.Revision, StringComparison.Ordinal))
        {
            return new PresentationGraphMutationResult(
                source,
                Succeeded: false,
                Changed: false,
                new[]
                {
                    new PresentationMutationDiagnostic(
                        "presentation.stored_revision_mismatch",
                        $"Stored revision '{storedRevision}' does not match normalized revision '{source.Revision}'.")
                });
        }

        PresentationGraphMutationResult result = new PresentationGraphMutationService().Apply(source, changeSet);
        if (!result.Succeeded || !result.Changed)
            return result;
        if (!HasEnabledRuntimeLeaf(result.Document))
        {
            return new PresentationGraphMutationResult(
                source,
                Succeeded: false,
                Changed: false,
                new[]
                {
                    new PresentationMutationDiagnostic(
                        "presentation.empty_runtime_plan",
                        "Poison Spear must keep at least one enabled runtime presentation leaf.")
                });
        }

        presentation.Set(
            "AuthoringNodeEnabled",
            result.Document.Nodes.Select(node => node.Enabled ? 1 : 0).ToArray());
        presentation.Set(
            "AuthoringNodePositions",
            result.Document.Nodes.Select(node => new Vector2(node.Position.X, node.Position.Y)).ToArray());
        SynchronizeRuntimePlan(presentation, result.Document);
        presentation.Set("Revision", result.Document.Revision);
        return result;
    }

    public static void ValidateStoredRevision(Resource presentation)
    {
        PresentationGraphDocument document = Read(presentation);
        string storedRevision = presentation.Get("Revision").AsString();
        if (!string.Equals(storedRevision, document.Revision, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Presentation revision mismatch: stored '{storedRevision}', normalized '{document.Revision}'.");
        }
    }

    /// <summary>
    /// Saves one resource and restores the previous bytes if save or uncached reload validation fails.
    /// </summary>
    public static void SaveWithRollback(
        Resource presentation,
        string path,
        Func<Resource, bool>? postSaveValidator = null)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Presentation path is required.", nameof(path));
        ValidateStoredRevision(presentation);

        string absolutePath = ProjectSettings.GlobalizePath(path);
        byte[]? backup = File.Exists(absolutePath) ? File.ReadAllBytes(absolutePath) : null;
        string uidText = ResourceUid.PathToUid(path);
        long existingUid = uidText.StartsWith("uid://", StringComparison.Ordinal)
            ? ResourceUid.TextToId(uidText)
            : ResourceUid.InvalidId;
        try
        {
            Error error = ResourceSaver.Save(presentation, path);
            if (error != Error.Ok)
                throw new InvalidOperationException($"ResourceSaver failed for '{path}': {error}.");
            if (existingUid != ResourceUid.InvalidId)
            {
                Error uidError = ResourceSaver.SetUid(path, existingUid);
                if (uidError != Error.Ok)
                {
                    throw new InvalidOperationException(
                        $"ResourceSaver could not preserve the UID for '{path}': {uidError}.");
                }
            }

            Resource? reloaded = ResourceLoader.Load<Resource>(path, string.Empty, ResourceLoader.CacheMode.Ignore);
            if (reloaded is null)
                throw new InvalidOperationException($"Saved presentation cannot be reloaded: {path}.");
            ValidateStoredRevision(reloaded);
            if (postSaveValidator is not null && !postSaveValidator(reloaded))
                throw new InvalidOperationException($"Post-save validation rejected presentation: {path}.");
        }
        catch
        {
            if (backup is null)
            {
                if (File.Exists(absolutePath))
                    File.Delete(absolutePath);
            }
            else
            {
                File.WriteAllBytes(absolutePath, backup);
            }
            throw;
        }
    }

    private static bool HasEnabledRuntimeLeaf(PresentationGraphDocument document) =>
        document.Nodes.Any(node => node.Enabled && IsRuntimeLeaf(node.NodeTypeId));

    private static void SynchronizeRuntimePlan(Resource presentation, PresentationGraphDocument document)
    {
        PresentationNodeDocument[] runtimeLeaves = document.Nodes
            .Where(node => node.Enabled && IsRuntimeLeaf(node.NodeTypeId))
            .ToArray();
        string[] runtimeIds = new[] { RuntimeRootNodeId }
            .Concat(runtimeLeaves.Select(node => node.NodeId))
            .ToArray();
        string[] runtimeTypes = new[] { "sequence" }
            .Concat(runtimeLeaves.Select(node => node.NodeTypeId switch
            {
                "PresentationUnitTweenNodeRecord" => "unit.tween.ranged",
                "PresentationProjectileNodeRecord" => "projectile.flight-impact",
                _ => throw new InvalidOperationException($"Unsupported runtime leaf '{node.NodeTypeId}'.")
            }))
            .ToArray();
        string[] runtimeChildren = new[] { string.Join(',', runtimeLeaves.Select(node => node.NodeId)) }
            .Concat(runtimeLeaves.Select(_ => string.Empty))
            .ToArray();
        _ = PoisonSpearPresentationResource.BuildExecutionPlan(
            document.SchemaVersion,
            RuntimeRootNodeId,
            runtimeIds,
            runtimeTypes,
            runtimeChildren);
        presentation.Set("NodeIds", runtimeIds);
        presentation.Set("NodeTypes", runtimeTypes);
        presentation.Set("NodeChildren", runtimeChildren);
        presentation.Set("PlanRootNodeId", RuntimeRootNodeId);
    }

    private static bool IsRuntimeLeaf(string nodeTypeId) => nodeTypeId is
        "PresentationUnitTweenNodeRecord" or "PresentationProjectileNodeRecord";
}
#endif
