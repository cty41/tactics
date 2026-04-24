using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.UI
{
    /// <summary>
    /// Draws all map node connection lines in a single VisualElement using Painter2D.
    /// </summary>
    public sealed class MapConnectionLinesElement : VisualElement
    {
        private readonly List<MapLineConnection> _connections = new List<MapLineConnection>();
        private float _lineWidth = 4f;

        public MapConnectionLinesElement()
        {
            generateVisualContent += OnGenerateVisualContent;
        }

        internal void SetConnections(IEnumerable<MapLineConnection> connections)
        {
            _connections.Clear();
            if (connections != null)
                _connections.AddRange(connections);
            MarkDirtyRepaint();
        }

        public void Refresh()
        {
            MarkDirtyRepaint();
        }

        public float LineWidth
        {
            get => _lineWidth;
            set
            {
                _lineWidth = value;
                MarkDirtyRepaint();
            }
        }

        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            if (_connections.Count == 0) return;

            var painter = mgc.painter2D;
            painter.lineWidth = _lineWidth;
            painter.lineCap = LineCap.Round;

            foreach (var conn in _connections)
            {
                painter.strokeColor = conn.Color;
                painter.BeginPath();
                painter.MoveTo(conn.FromPoint);
                painter.LineTo(conn.ToPoint);
                painter.Stroke();
            }
        }
    }
}
