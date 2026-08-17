#if TOOLS
using Godot;
using Tactics.Application.Authoring;
using Tactics.Core.Runs;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

public static class MapAuthoringEditorService
{
    public static MapAuthoringDocument Read(PureRunMapResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        int count = resource.NodeIds.Length;
        if (new[]
            {
                resource.NodeLayers.Length, resource.NodeKinds.Length, resource.NodeContentIds.Length,
                resource.NodeTitles.Length, resource.NodeLanes.Length
            }.Any(value => value != count) ||
            resource.ConnectionFromNodeIds.Length != resource.ConnectionToNodeIds.Length)
            throw new InvalidOperationException("Map resource arrays have inconsistent lengths.");
        return new MapAuthoringDocument(
            resource.ContentIdValue,
            resource.LayoutVersion,
            Enumerable.Range(0, count).Select(index => new MapAuthoringNode(
                resource.NodeIds[index],
                resource.NodeLayers[index],
                Enum.Parse<PureRunNodeKind>(resource.NodeKinds[index], false),
                resource.NodeContentIds[index],
                resource.NodeTitles[index],
                resource.NodeLanes[index])),
            Enumerable.Range(0, resource.ConnectionFromNodeIds.Length).Select(index =>
                new MapAuthoringConnection(
                    resource.ConnectionFromNodeIds[index],
                    resource.ConnectionToNodeIds[index])));
    }

    public static void Write(PureRunMapResource resource, MapAuthoringDocument document)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(document);
        if (!string.Equals(resource.ContentIdValue, document.ContentId, StringComparison.Ordinal))
            throw new InvalidOperationException("Map document identity does not match the Resource.");
        resource.LayoutVersion = document.LayoutVersion;
        resource.NodeIds = document.Nodes.Select(value => value.NodeId).ToArray();
        resource.NodeLayers = document.Nodes.Select(value => value.Layer).ToArray();
        resource.NodeKinds = document.Nodes.Select(value => value.Kind.ToString()).ToArray();
        resource.NodeContentIds = document.Nodes.Select(value => value.ContentId).ToArray();
        resource.NodeTitles = document.Nodes.Select(value => value.Title).ToArray();
        resource.NodeLanes = document.Nodes.Select(value => value.Lane).ToArray();
        resource.ConnectionFromNodeIds = document.Connections.Select(value => value.FromNodeId).ToArray();
        resource.ConnectionToNodeIds = document.Connections.Select(value => value.ToNodeId).ToArray();
        PureRunMapWorkbench.ValidateResource(resource);
    }
}
#endif
