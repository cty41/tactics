using System.Collections.Generic;

namespace Tactics.Common.Skills.Graph
{
    /// <summary>
    /// 技能图运行时定义。
    /// 从 SkillGraphAsset 生成，提供轻量、索引化的运行时视图。
    /// </summary>
    public class SkillGraphRuntimeDefinition
    {
        private readonly Dictionary<string, SkillGraphNodeRecord> _nodes = new();
        private readonly Dictionary<string, List<SkillGraphEdgeRecord>> _edgesFrom = new();
        private readonly string _entryNodeId;

        public string EntryNodeId => _entryNodeId;
        public int NodeCount => _nodes.Count;

        private SkillGraphRuntimeDefinition(string entryNodeId)
        {
            _entryNodeId = entryNodeId;
        }

        /// <summary>
        /// 从 SkillGraphAsset 创建运行时定义。
        /// </summary>
        public static SkillGraphRuntimeDefinition FromAsset(SkillGraphAsset asset)
        {
            var entry = asset.FindEntryNode();
            var def = new SkillGraphRuntimeDefinition(entry?.NodeId);

            for (int i = 0; i < asset.Nodes.Count; i++)
            {
                var node = asset.Nodes[i];
                def._nodes[node.NodeId] = node;
            }

            for (int i = 0; i < asset.Edges.Count; i++)
            {
                var edge = asset.Edges[i];
                if (!def._edgesFrom.TryGetValue(edge.SourceNodeId, out var list))
                {
                    list = new List<SkillGraphEdgeRecord>();
                    def._edgesFrom[edge.SourceNodeId] = list;
                }
                list.Add(edge);
            }

            return def;
        }

        public SkillGraphNodeRecord GetNode(string nodeId)
        {
            _nodes.TryGetValue(nodeId, out var node);
            return node;
        }

        public List<SkillGraphEdgeRecord> GetEdgesFrom(string nodeId)
        {
            _edgesFrom.TryGetValue(nodeId, out var edges);
            return edges ?? EmptyEdges;
        }

        public SkillGraphNodeRecord GetFirstTarget(string nodeId, SkillGraphPortType portType = SkillGraphPortType.Default)
        {
            var edges = GetEdgesFrom(nodeId);
            for (int i = 0; i < edges.Count; i++)
            {
                if (edges[i].PortType == portType || portType == SkillGraphPortType.Default)
                {
                    var target = GetNode(edges[i].TargetNodeId);
                    if (target != null) return target;
                }
            }
            return null;
        }

        private static readonly List<SkillGraphEdgeRecord> EmptyEdges = new();
    }
}
