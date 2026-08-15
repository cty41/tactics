using Godot;
using Tactics.Core.Content;
using Tactics.Core.Runs;

namespace Tactics.Godot.Adapter.Runtime;

[Tool]
[GlobalClass]
public partial class PureRunMapResource : Resource
{
    [Export] public string ContentIdValue { get; set; } = string.Empty;
    [Export] public int LayoutVersion { get; set; } = 3;
    [Export] public string[] NodeIds { get; set; } = Array.Empty<string>();
    [Export] public int[] NodeLayers { get; set; } = Array.Empty<int>();
    [Export] public string[] NodeKinds { get; set; } = Array.Empty<string>();
    [Export] public string[] NodeContentIds { get; set; } = Array.Empty<string>();
    [Export] public string[] NodeTitles { get; set; } = Array.Empty<string>();
    [Export] public float[] NodeLanes { get; set; } = Array.Empty<float>();
    [Export] public string[] ConnectionFromNodeIds { get; set; } = Array.Empty<string>();
    [Export] public string[] ConnectionToNodeIds { get; set; } = Array.Empty<string>();

    public PureRunMapDefinition ToCoreDefinition()
    {
        int count = NodeIds.Length;
        if (new[] { NodeLayers.Length, NodeKinds.Length, NodeContentIds.Length, NodeTitles.Length, NodeLanes.Length }
            .Any(value => value != count) || ConnectionFromNodeIds.Length != ConnectionToNodeIds.Length)
            throw new InvalidOperationException("Map resource arrays have inconsistent lengths.");
        var nodes = Enumerable.Range(0, count).Select(index => new PureRunMapNodeDefinition(NodeIds[index],
            NodeLayers[index], Enum.Parse<PureRunNodeKind>(NodeKinds[index], false),
            new ContentId(NodeContentIds[index]), NodeTitles[index], NodeLanes[index])).ToArray();
        var connections = Enumerable.Range(0, ConnectionFromNodeIds.Length).Select(index =>
            new PureRunMapConnectionDefinition(ConnectionFromNodeIds[index], ConnectionToNodeIds[index])).ToArray();
        return new PureRunMapDefinition(new ContentId(ContentIdValue), LayoutVersion, nodes, connections);
    }
}
