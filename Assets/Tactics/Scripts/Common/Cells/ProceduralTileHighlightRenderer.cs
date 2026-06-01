using System;
using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Cells;
using Tactics.Common.Utilities;
using Tactics.Runtime.Utilities;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

namespace Tactics.Cells
{
    public enum TileHighlightType
    {
        Highlighted,
        Reachable,
        Path,
        PathEnd,
        AoE,
    }

    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class ProceduralTileHighlightRenderer : MonoBehaviour
    {
        [FormerlySerializedAs("_dataLayer")]
        [SerializeField] private Tilemap _gridLayer;

        [SerializeField] private Color _highlightedColor = new Color(1f, 1f, 1f, 0.5f);
        [SerializeField] private Color _reachableMoveColor = new Color(0.2f, 0.8f, 1f, 0.5f);
        [SerializeField] private Color _reachableAttackColor = new Color(1f, 0.3f, 0.2f, 0.5f);
        [SerializeField] private Color _pathColor = new Color(1f, 0.85f, 0.2f, 0.55f);
        [SerializeField] private Color _pathEndColor = new Color(1f, 0.5f, 0f, 0.7f);
        [SerializeField] private Color _aoeColor = new Color(1f, 0.5f, 0f, 0.5f);

        [SerializeField] private bool _enablePulse = false;
        [SerializeField] private float _pulseSpeed = 2f;
        [SerializeField] private float _pulseMinOpacity = 0.15f;

        [SerializeField] private int _sortingOrder = 10;
        [SerializeField] private Material _overrideMaterial;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        private Material _material;

        private bool _useMoveColorForReachable = true;
        private readonly Dictionary<TileHighlightType, HashSet<Vector2IntImpl>> _highlights = new();

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();

            // Ensure SortingGroup is present so sortingOrder works for MeshRenderer in URP/2D
            var sortingGroup = GetComponent<SortingGroup>();
            if (sortingGroup == null)
            {
                sortingGroup = gameObject.AddComponent<SortingGroup>();
            }
            sortingGroup.sortingOrder = _sortingOrder;

            _mesh = new Mesh { name = "TileHighlightMesh" };
            _meshFilter.mesh = _mesh;

            SetupMaterial();
            _meshRenderer.sortingOrder = _sortingOrder;
            _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
        }

        public void SetDataLayer(Tilemap dataLayer)
        {
            _gridLayer = dataLayer;
        }

        private void SetupMaterial()
        {
            if (_overrideMaterial != null)
            {
                _material = new Material(_overrideMaterial);
            }
            else
            {
                Shader shader = Shader.Find("Custom/TileHighlightShader");
                if (shader == null)
                {
                    shader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (shader == null)
                    {
                        shader = Shader.Find("Sprites/Default");
                    }
                }
                _material = new Material(shader);
            }

            _material.SetFloat("_PulseSpeed", _enablePulse ? _pulseSpeed : 0f);
            _material.SetFloat("_PulseMinOpacity", _pulseMinOpacity);
            _material.enableInstancing = true;
            _material.renderQueue = 3001; // Render after terrain transparent (3000)
            _meshRenderer.material = _material;
        }

        private void OnValidate()
        {
            if (_meshRenderer != null)
                _meshRenderer.sortingOrder = _sortingOrder;
            if (_material != null)
            {
                _material.SetFloat("_PulseSpeed", _enablePulse ? _pulseSpeed : 0f);
                _material.SetFloat("_PulseMinOpacity", _pulseMinOpacity);
            }
        }

        public void SetHighlights(IEnumerable<ICell> cells, TileHighlightType type)
        {
            if (cells == null) return;

            if (!_highlights.TryGetValue(type, out var set))
            {
                set = new HashSet<Vector2IntImpl>();
                _highlights[type] = set;
            }

            set.Clear();
            foreach (var cell in cells)
            {
                set.Add(cell.GridCoordinates);
            }

            RebuildMesh();
        }

        public void AddHighlights(IEnumerable<ICell> cells, TileHighlightType type)
        {
            if (cells == null) return;

            if (!_highlights.TryGetValue(type, out var set))
            {
                set = new HashSet<Vector2IntImpl>();
                _highlights[type] = set;
            }

            bool changed = false;
            foreach (var cell in cells)
            {
                changed |= set.Add(cell.GridCoordinates);
            }

            if (changed)
            {
                RebuildMesh();
            }
        }

        public void SetPathHighlights(IEnumerable<ICell> cells)
        {
            if (cells == null) return;

            var list = cells.ToList();
            if (list.Count == 0)
            {
                Clear(TileHighlightType.Path);
                Clear(TileHighlightType.PathEnd);
                return;
            }

            var pathSet = new HashSet<Vector2IntImpl>();
            foreach (var cell in list)
            {
                pathSet.Add(cell.GridCoordinates);
            }
            _highlights[TileHighlightType.Path] = pathSet;

            var endSet = new HashSet<Vector2IntImpl>
            {
                list[list.Count - 1].GridCoordinates
            };
            _highlights[TileHighlightType.PathEnd] = endSet;

            RebuildMesh();
        }

        public void Clear(TileHighlightType type)
        {
            if (_highlights.Remove(type))
            {
                RebuildMesh();
            }
        }

        public void Clear()
        {
            if (_highlights.Count > 0)
            {
                _highlights.Clear();
                RebuildMesh();
            }
        }

        public void RemoveHighlights(IEnumerable<ICell> cells)
        {
            if (cells == null) return;

            bool changed = false;
            foreach (var cell in cells)
            {
                var coord = cell.GridCoordinates;
                foreach (var set in _highlights.Values)
                {
                    changed |= set.Remove(coord);
                }
            }

            if (changed)
            {
                RebuildMesh();
            }
        }

        public void RemoveHighlight(ICell cell)
        {
            if (cell == null) return;

            bool changed = false;
            var coord = cell.GridCoordinates;
            foreach (var set in _highlights.Values)
            {
                changed |= set.Remove(coord);
            }

            if (changed)
            {
                RebuildMesh();
            }
        }

        public void RemoveHighlightsOfType(IEnumerable<ICell> cells, TileHighlightType type)
        {
            if (cells == null) return;
            if (!_highlights.TryGetValue(type, out var set)) return;

            bool changed = false;
            foreach (var cell in cells)
            {
                changed |= set.Remove(cell.GridCoordinates);
            }

            if (changed)
            {
                RebuildMesh();
            }
        }

        public void RemoveHighlightOfType(ICell cell, TileHighlightType type)
        {
            if (cell == null) return;
            if (!_highlights.TryGetValue(type, out var set)) return;

            if (set.Remove(cell.GridCoordinates))
            {
                RebuildMesh();
            }
        }

        public void SetReachableMovementMode(bool isMovement)
        {
            _useMoveColorForReachable = isMovement;
            if (_highlights.ContainsKey(TileHighlightType.Reachable))
            {
                RebuildMesh();
            }
        }

        public void SetAoEHighlights(IEnumerable<ICell> cells)
        {
            if (cells == null) return;

            if (!_highlights.TryGetValue(TileHighlightType.AoE, out var set))
            {
                set = new HashSet<Vector2IntImpl>();
                _highlights[TileHighlightType.AoE] = set;
            }

            set.Clear();
            foreach (var cell in cells)
            {
                set.Add(cell.GridCoordinates);
            }

            RebuildMesh();
        }

        private Color GetColorForType(TileHighlightType type)
        {
            return type switch
            {
                TileHighlightType.Highlighted => _highlightedColor,
                TileHighlightType.Reachable => _useMoveColorForReachable ? _reachableMoveColor : _reachableAttackColor,
                TileHighlightType.Path => _pathColor,
                TileHighlightType.PathEnd => _pathEndColor,
                TileHighlightType.AoE => _aoeColor,
                _ => Color.white,
            };
        }

        private void RebuildMesh()
        {
            if (_gridLayer == null || _highlights.Count == 0)
            {
                _mesh.Clear();
                return;
            }

            int totalCells = _highlights.Values.Sum(s => s.Count);
            var vertices = new Vector3[totalCells * 4];
            var colors = new Color[totalCells * 4];
            var triangles = new int[totalCells * 6];

            int cellIndex = 0;
            foreach (var kvp in _highlights)
            {
                TileHighlightType type = kvp.Key;
                Color color = GetColorForType(type);

                foreach (var coord in kvp.Value)
                {
                    Vector3Int pos = new Vector3Int(coord.x, coord.y, 0);

                    Vector3 worldCenter = _gridLayer.GetCellCenterWorld(pos);
                    Vector3 center = transform.InverseTransformPoint(worldCenter);
                    center.y += 0.02f;

                    Vector3 worldRight = _gridLayer.GetCellCenterWorld(pos + Vector3Int.right);
                    Vector3 worldUp = _gridLayer.GetCellCenterWorld(pos + Vector3Int.up);
                    Vector3 localRight = transform.InverseTransformPoint(worldRight);
                    Vector3 localUp = transform.InverseTransformPoint(worldUp);
                    Vector3 dx = (localRight - center) * 0.5f;
                    Vector3 dy = (localUp - center) * 0.5f;

                    int vBase = cellIndex * 4;
                    int tBase = cellIndex * 6;

                    vertices[vBase + 0] = center + dx + dy;
                    vertices[vBase + 1] = center + dx - dy;
                    vertices[vBase + 2] = center - dx - dy;
                    vertices[vBase + 3] = center - dx + dy;

                    colors[vBase + 0] = color;
                    colors[vBase + 1] = color;
                    colors[vBase + 2] = color;
                    colors[vBase + 3] = color;

                    triangles[tBase + 0] = vBase + 0;
                    triangles[tBase + 1] = vBase + 1;
                    triangles[tBase + 2] = vBase + 2;
                    triangles[tBase + 3] = vBase + 0;
                    triangles[tBase + 4] = vBase + 2;
                    triangles[tBase + 5] = vBase + 3;

                    cellIndex++;
                }
            }

            _mesh.Clear();
            _mesh.vertices = vertices;
            _mesh.colors = colors;
            _mesh.triangles = triangles;
            _mesh.RecalculateBounds();
        }
    }
}
