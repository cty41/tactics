#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Skills.Graph;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.Editor.PresentationGraph
{
    internal sealed class PresentationGraphView : GraphView
    {
        private readonly Dictionary<string, PresentationNodeView> _views = new();
        private BattlePresentationGraph _graph;
        private bool _isReloading;

        internal event Action<PresentationNodeRecord> NodeSelected;

        internal PresentationGraphView()
        {
            Insert(0, new GridBackground());
            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            RegisterCallback<ContextualMenuPopulateEvent>(PopulateContextMenu);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            graphViewChanged = OnGraphChanged;
            style.flexGrow = 1f;
        }

        internal void Load(BattlePresentationGraph graph)
        {
            _graph = graph;
            _isReloading = true;
            try
            {
                DeleteElements(graphElements.ToList());
                _views.Clear();
            }
            finally
            {
                _isReloading = false;
            }
            if (_graph == null)
                return;

            foreach (PresentationNodeRecord record in _graph.Nodes.Where(node => node != null))
                AddNodeView(record);
            foreach (PresentationEdgeRecord record in _graph.Edges.Where(edge => edge != null))
            {
                if (!_views.TryGetValue(record.SourceNodeId, out PresentationNodeView source) ||
                    !_views.TryGetValue(record.TargetNodeId, out PresentationNodeView target) ||
                    source.Output == null || target.Input == null)
                {
                    continue;
                }
                Edge edge = source.Output.ConnectTo(target.Input);
                edge.userData = record.EdgeId;
                AddElement(edge);
            }
        }

        internal void SavePositions()
        {
            if (_graph == null)
                return;
            bool changed = _views.Any(pair =>
            {
                PresentationNodeRecord record = _graph.FindNode(pair.Key);
                return record != null && record.Position != pair.Value.GetPosition().position;
            });
            if (!changed)
                return;
            Undo.RecordObject(_graph, "Move Presentation Nodes");
            foreach ((string id, PresentationNodeView view) in _views)
            {
                PresentationNodeRecord record = _graph.FindNode(id);
                if (record != null)
                    record.Position = view.GetPosition().position;
            }
            EditorUtility.SetDirty(_graph);
        }

        private void PopulateContextMenu(ContextualMenuPopulateEvent evt)
        {
            if (_graph == null)
                return;
            evt.menu.AppendAction(
                "Duplicate Selection",
                _ => DuplicateSelection(),
                _ => selection.OfType<PresentationNodeView>().Any()
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
            Vector3 point = viewTransform.matrix.inverse.MultiplyPoint(evt.localMousePosition);
            foreach (PresentationNodeType type in Enum.GetValues(typeof(PresentationNodeType)))
            {
                PresentationNodeType captured = type;
                evt.menu.AppendAction($"Add/{FormatTitle(type)}", _ =>
                {
                    Undo.RecordObject(_graph, $"Add {captured}");
                    PresentationNodeRecord record = _graph.AddNode(captured, point);
                    AddNodeView(record);
                    EditorUtility.SetDirty(_graph);
                });
            }
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if ((evt.ctrlKey || evt.commandKey) && evt.keyCode == KeyCode.D)
            {
                DuplicateSelection();
                evt.StopPropagation();
            }
        }

        private void DuplicateSelection()
        {
            if (_graph == null)
                return;
            List<PresentationNodeView> selected = selection
                .OfType<PresentationNodeView>()
                .ToList();
            if (selected.Count == 0)
                return;

            Undo.RecordObject(_graph, "Duplicate Presentation Nodes");
            var replacements = new Dictionary<string, PresentationNodeRecord>();
            foreach (PresentationNodeView view in selected)
            {
                PresentationNodeRecord source = _graph.FindNode(view.NodeId);
                PresentationNodeRecord clone = CloneNode(source);
                if (clone == null)
                    continue;
                clone.NodeId = Guid.NewGuid().ToString("N");
                clone.Position = source.Position + new Vector2(28f, 28f);
                _graph.Nodes.Add(clone);
                replacements[source.NodeId] = clone;
            }

            foreach (PresentationEdgeRecord edge in _graph.Edges.ToList())
            {
                if (replacements.TryGetValue(edge.SourceNodeId, out PresentationNodeRecord source) &&
                    replacements.TryGetValue(edge.TargetNodeId, out PresentationNodeRecord target))
                {
                    _graph.AddEdge(source.NodeId, target.NodeId);
                }
            }
            foreach (PresentationNodeRecord clone in replacements.Values)
            {
                if (clone is PresentationForkNodeRecord fork &&
                    replacements.TryGetValue(fork.JoinNodeId, out PresentationNodeRecord join))
                {
                    fork.JoinNodeId = join.NodeId;
                }
            }
            EditorUtility.SetDirty(_graph);
            Load(_graph);
        }

        private static PresentationNodeRecord CloneNode(PresentationNodeRecord source)
        {
            if (source == null)
                return null;
            PresentationNodeRecord clone = PresentationNodeRecord.Create(source.NodeType);
            clone.Enabled = source.Enabled;
            switch (source)
            {
                case PresentationEntryNodeRecord value:
                    ((PresentationEntryNodeRecord)clone).Cue = value.Cue;
                    break;
                case PresentationUnitTweenNodeRecord value:
                    ((PresentationUnitTweenNodeRecord)clone).Action = value.Action;
                    ((PresentationUnitTweenNodeRecord)clone).EmitReleaseMarker = value.EmitReleaseMarker;
                    break;
                case PresentationProjectileNodeRecord value:
                    ((PresentationProjectileNodeRecord)clone).Profile = value.Profile;
                    ((PresentationProjectileNodeRecord)clone).Speed = value.Speed;
                    ((PresentationProjectileNodeRecord)clone).FallbackTravelTime = value.FallbackTravelTime;
                    ((PresentationProjectileNodeRecord)clone).EmitImpactMarker = value.EmitImpactMarker;
                    break;
                case PresentationPrefabFxNodeRecord value:
                    ((PresentationPrefabFxNodeRecord)clone).Profile = value.Profile;
                    break;
                case PresentationProceduralVfxNodeRecord value:
                    ((PresentationProceduralVfxNodeRecord)clone).Recipe = value.Recipe;
                    ((PresentationProceduralVfxNodeRecord)clone).Cue = value.Cue;
                    break;
                case PresentationDelayNodeRecord value:
                    ((PresentationDelayNodeRecord)clone).Duration = value.Duration;
                    break;
                case PresentationMarkerNodeRecord value:
                    ((PresentationMarkerNodeRecord)clone).Marker = value.Marker;
                    break;
                case PresentationForkNodeRecord value:
                    ((PresentationForkNodeRecord)clone).JoinNodeId = value.JoinNodeId;
                    break;
            }
            return clone;
        }

        private void AddNodeView(PresentationNodeRecord record)
        {
            if (record == null)
                return;
            bool hasInput = record is not PresentationEntryNodeRecord;
            bool hasOutput = record is not PresentationFinishNodeRecord;
            bool multiOutput = record is PresentationForkNodeRecord;
            var view = new PresentationNodeView(
                record,
                FormatTitle(record.NodeType),
                hasInput,
                hasOutput,
                multiOutput);
            view.SetPosition(new Rect(record.Position, new Vector2(210f, 110f)));
            view.Selected += () => NodeSelected?.Invoke(record);
            _views[record.NodeId] = view;
            AddElement(view);
        }

        private GraphViewChange OnGraphChanged(GraphViewChange change)
        {
            if (_graph == null || _isReloading)
                return change;
            if (change.elementsToRemove != null)
            {
                bool recorded = false;
                foreach (GraphElement element in change.elementsToRemove)
                {
                    if (!recorded)
                    {
                        Undo.RecordObject(_graph, "Delete Presentation Graph Element");
                        recorded = true;
                    }
                    if (element is Edge edge && edge.userData is string edgeId)
                        _graph.RemoveEdge(edgeId);
                    else if (element is PresentationNodeView node)
                    {
                        _graph.RemoveNode(node.NodeId);
                        _views.Remove(node.NodeId);
                    }
                }
                if (recorded)
                    EditorUtility.SetDirty(_graph);
            }

            if (change.edgesToCreate != null)
            {
                var accepted = new List<Edge>();
                foreach (Edge edge in change.edgesToCreate)
                {
                    if (edge.output?.node is not PresentationNodeView source ||
                        edge.input?.node is not PresentationNodeView target)
                    {
                        continue;
                    }
                    Undo.RecordObject(_graph, "Connect Presentation Nodes");
                    PresentationEdgeRecord record = _graph.AddEdge(source.NodeId, target.NodeId);
                    if (record == null)
                        continue;
                    edge.userData = record.EdgeId;
                    accepted.Add(edge);
                    EditorUtility.SetDirty(_graph);
                }
                change.edgesToCreate = accepted;
            }
            return change;
        }

        private static string FormatTitle(object value)
        {
            string text = value.ToString();
            for (int index = text.Length - 1; index > 0; index--)
            {
                if (char.IsUpper(text[index]) && !char.IsUpper(text[index - 1]))
                    text = text.Insert(index, " ");
            }
            return text;
        }

        private sealed class PresentationNodeView : Node
        {
            internal PresentationNodeView(
                PresentationNodeRecord record,
                string titleText,
                bool hasInput,
                bool hasOutput,
                bool multiOutput)
            {
                NodeId = record.NodeId;
                title = titleText;
                if (hasInput)
                {
                    Input = InstantiatePort(
                        Orientation.Horizontal,
                        Direction.Input,
                        Port.Capacity.Multi,
                        typeof(bool));
                    Input.portName = string.Empty;
                    inputContainer.Add(Input);
                }
                if (hasOutput)
                {
                    Output = InstantiatePort(
                        Orientation.Horizontal,
                        Direction.Output,
                        multiOutput ? Port.Capacity.Multi : Port.Capacity.Single,
                        typeof(bool));
                    Output.portName = string.Empty;
                    outputContainer.Add(Output);
                }
                var summary = new Label(Describe(record));
                summary.style.fontSize = 9f;
                summary.style.whiteSpace = WhiteSpace.Normal;
                mainContainer.Add(summary);
                RefreshPorts();
                RefreshExpandedState();
            }

            internal event Action Selected;
            internal string NodeId { get; }
            internal Port Input { get; }
            internal Port Output { get; }

            public override void OnSelected()
            {
                base.OnSelected();
                Selected?.Invoke();
            }

            private static string Describe(PresentationNodeRecord record)
            {
                return record switch
                {
                    PresentationEntryNodeRecord entry => entry.Cue.ToString(),
                    PresentationUnitTweenNodeRecord tween => tween.Action.ToString(),
                    PresentationProjectileNodeRecord projectile => projectile.Profile?.name ?? "No Profile",
                    PresentationPrefabFxNodeRecord fx => fx.Profile?.name ?? "No Profile",
                    PresentationProceduralVfxNodeRecord procedural =>
                        $"{procedural.Cue}: {procedural.Recipe?.name ?? "No Recipe"}",
                    PresentationDelayNodeRecord delay => $"{delay.Duration:0.###}s",
                    PresentationMarkerNodeRecord marker => marker.Marker.ToString(),
                    PresentationForkNodeRecord fork => $"Join: {fork.JoinNodeId}",
                    _ => string.Empty
                };
            }
        }
    }
}
#endif
