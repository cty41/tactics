namespace Tactics.Application.Presentation;

/// <summary>
/// Validates and applies typed presentation graph mutations without Unity, Godot, or editor APIs.
/// </summary>
public sealed class PresentationGraphMutationService
{
    public PresentationGraphMutationResult Apply(
        PresentationGraphDocument source,
        PresentationGraphChangeSet changeSet)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(changeSet);
        if (!string.Equals(source.Revision, changeSet.ExpectedRevision, StringComparison.Ordinal))
        {
            return Failed(
                source,
                "presentation.revision_conflict",
                $"Expected revision '{changeSet.ExpectedRevision}', actual '{source.Revision}'.");
        }

        PresentationNodeDocument[] nodes = source.Nodes.ToArray();
        bool changed = false;
        foreach (PresentationGraphOperation operation in changeSet.Operations)
        {
            string nodeId = operation switch
            {
                SetPresentationNodeEnabledOperation enabled => enabled.NodeId,
                SetPresentationNodePositionOperation position => position.NodeId,
                _ => string.Empty
            };
            int index = nodes
                .Select((node, nodeIndex) => (node, nodeIndex))
                .Where(item => string.Equals(item.node.NodeId, nodeId, StringComparison.Ordinal))
                .Select(item => item.nodeIndex)
                .DefaultIfEmpty(-1)
                .Single();
            if (index < 0 && operation is SetPresentationNodeEnabledOperation or SetPresentationNodePositionOperation)
            {
                return Failed(
                    source,
                    "presentation.node_not_found",
                    $"Node '{nodeId}' does not exist.");
            }

            switch (operation)
            {
                case SetPresentationNodeEnabledOperation setEnabled:
                {
                    if (nodes[index].Enabled == setEnabled.Enabled)
                        break;
                    nodes[index] = nodes[index] with { Enabled = setEnabled.Enabled };
                    changed = true;
                    break;
                }
                case SetPresentationNodePositionOperation setPosition:
                {
                    if (!setPosition.Position.IsFinite)
                    {
                        return Failed(
                            source,
                            "presentation.invalid_position",
                            $"Node '{setPosition.NodeId}' position must be finite.");
                    }
                    if (nodes[index].Position == setPosition.Position)
                        break;
                    nodes[index] = nodes[index] with { Position = setPosition.Position };
                    changed = true;
                    break;
                }
                default:
                    return Failed(
                        source,
                        "presentation.unsupported_operation",
                        $"Operation '{operation.GetType().Name}' is not supported.");
            }
        }

        if (nodes.Select(node => node.Position).Distinct().Count() != nodes.Length)
        {
            return Failed(
                source,
                "presentation.position_overlap",
                "Presentation node authoring positions must not overlap.");
        }

        PresentationGraphDocument candidate = changed
            ? source.WithNodes(nodes)
            : source;

        return new PresentationGraphMutationResult(
            candidate,
            Succeeded: true,
            Changed: changed,
            Array.Empty<PresentationMutationDiagnostic>());
    }

    private static PresentationGraphMutationResult Failed(
        PresentationGraphDocument source,
        string code,
        string message) => new(
            source,
            Succeeded: false,
            Changed: false,
            new[] { new PresentationMutationDiagnostic(code, message) });
}
