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
        SpearLocation,
        SpearPickup,
        UnitFriendly,
        UnitSelected,
        UnitFinished,
        UnitTargetable,
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
        [SerializeField] private Color _spearLocationColor = new Color(1f, 0.55f, 0.15f, 0.7f);
        [SerializeField] private Color _spearPickupColor = new Color(0.25f, 0.9f, 0.35f, 0.55f);
        [SerializeField] private Color _unitFriendlyColor = new Color(0.34f, 0.52f, 0.62f, 0.22f);
        [SerializeField] private Color _unitSelectedColor = new Color(0.95f, 0.66f, 0.24f, 0.45f);
        [SerializeField] private Color _unitFinishedColor = new Color(0.36f, 0.42f, 0.48f, 0.18f);
        [SerializeField] private Color _unitTargetableColor = new Color(0.82f, 0.30f, 0.24f, 0.34f);

        [SerializeField] private bool _enablePulse = false;
        [SerializeField] private float _pulseSpeed = 2f;
        [SerializeField] private float _pulseMinOpacity = 0.15f;

        [SerializeField] private int _sortingOrder = 10;
        [SerializeField] private Material _overrideMaterial;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        private Material _material;

        private GameObject _unitStateMeshObject;
        private Mesh _unitStateMesh;
        private MeshRenderer _unitStateMeshRenderer;
        private readonly Dictionary<int, UnitStateHighlightBinding> _unitStateHighlights = new();
        private readonly List<int> _invalidUnitStateIds = new();
        private readonly List<int> _orderedUnitStateIds = new();
        private Vector3[] _unitStateVertices = Array.Empty<Vector3>();
        private Color[] _unitStateColors = Array.Empty<Color>();
        private int[] _unitStateTriangles = Array.Empty<int>();

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
            EnsureUnitStateMesh();
        }

        public void SetDataLayer(Tilemap dataLayer)
        {
            _gridLayer = dataLayer;
            RebuildMesh();
            RebuildUnitStateMesh();
        }

        private void LateUpdate()
        {
            if (UnitStateGeometryChanged())
            {
                RebuildUnitStateMesh();
            }
        }

        private void OnDestroy()
        {
            _unitStateHighlights.Clear();
            if (_unitStateMesh == null)
                return;

            if (Application.isPlaying)
                Destroy(_unitStateMesh);
            else
                DestroyImmediate(_unitStateMesh);
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

        private void EnsureUnitStateMesh()
        {
            if (_unitStateMeshObject != null)
                return;

            _unitStateMeshObject = new GameObject("UnitStateHighlights");
            _unitStateMeshObject.transform.SetParent(transform, false);

            var meshFilter = _unitStateMeshObject.AddComponent<MeshFilter>();
            _unitStateMeshRenderer = _unitStateMeshObject.AddComponent<MeshRenderer>();
            _unitStateMesh = new Mesh { name = "UnitStateHighlightMesh" };
            meshFilter.mesh = _unitStateMesh;

            _unitStateMeshRenderer.sharedMaterial = _material;
            _unitStateMeshRenderer.sortingOrder = _sortingOrder;
            _unitStateMeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _unitStateMeshRenderer.receiveShadows = false;
        }

        private void OnValidate()
        {
            if (_meshRenderer != null)
                _meshRenderer.sortingOrder = _sortingOrder;
            if (_unitStateMeshRenderer != null)
                _unitStateMeshRenderer.sortingOrder = _sortingOrder;
            if (_material != null)
            {
                _material.SetFloat("_PulseSpeed", _enablePulse ? _pulseSpeed : 0f);
                _material.SetFloat("_PulseMinOpacity", _pulseMinOpacity);
            }
        }

        internal void SetUnitStateHighlight(int unitInstanceId, Transform unitRoot, TileHighlightType type)
        {
            if (unitRoot == null || !IsUnitStateType(type))
                return;

            EnsureUnitStateMesh();
            _unitStateHighlights[unitInstanceId] = new UnitStateHighlightBinding(unitRoot, type);
            RebuildUnitStateMesh();
        }

        internal void RemoveUnitStateHighlight(int unitInstanceId)
        {
            if (!_unitStateHighlights.Remove(unitInstanceId))
                return;

            RebuildUnitStateMesh();
        }

        internal int UnitStateHighlightCount => _unitStateHighlights.Count;

        internal Mesh UnitStateHighlightMesh => _unitStateMesh;

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
                TileHighlightType.SpearLocation => _spearLocationColor,
                TileHighlightType.SpearPickup => _spearPickupColor,
                TileHighlightType.UnitFriendly => _unitFriendlyColor,
                TileHighlightType.UnitSelected => _unitSelectedColor,
                TileHighlightType.UnitFinished => _unitFinishedColor,
                TileHighlightType.UnitTargetable => _unitTargetableColor,
                _ => Color.white,
            };
        }

        private static bool IsUnitStateType(TileHighlightType type)
        {
            return type is TileHighlightType.UnitFriendly
                or TileHighlightType.UnitSelected
                or TileHighlightType.UnitFinished
                or TileHighlightType.UnitTargetable;
        }

        private bool UnitStateGeometryChanged()
        {
            bool changed = false;
            _invalidUnitStateIds.Clear();
            foreach (var pair in _unitStateHighlights)
            {
                var binding = pair.Value;
                if (binding.UnitRoot == null)
                {
                    _invalidUnitStateIds.Add(pair.Key);
                    changed = true;
                    continue;
                }

                if ((binding.UnitRoot.position - binding.LastWorldPosition).sqrMagnitude > 0.00000001f)
                {
                    changed = true;
                }
            }

            foreach (int invalidId in _invalidUnitStateIds)
            {
                _unitStateHighlights.Remove(invalidId);
            }

            return changed;
        }

        private void RebuildUnitStateMesh()
        {
            if (_unitStateMesh == null)
                return;

            _invalidUnitStateIds.Clear();
            foreach (var pair in _unitStateHighlights)
            {
                if (pair.Value.UnitRoot == null)
                    _invalidUnitStateIds.Add(pair.Key);
            }

            foreach (int invalidId in _invalidUnitStateIds)
            {
                _unitStateHighlights.Remove(invalidId);
            }

            if (_gridLayer == null || _unitStateHighlights.Count == 0)
            {
                _unitStateMesh.Clear();
                return;
            }

            int highlightCount = _unitStateHighlights.Count;
            EnsureUnitStateBuffers(highlightCount);
            TilemapCellGeometry.GetCellBasisWorld(
                _gridLayer,
                Vector3Int.zero,
                out Vector3 xAxis,
                out Vector3 yAxis);

            _orderedUnitStateIds.Clear();
            _orderedUnitStateIds.AddRange(_unitStateHighlights.Keys);
            _orderedUnitStateIds.Sort();

            int highlightIndex = 0;
            foreach (int unitInstanceId in _orderedUnitStateIds)
            {
                var binding = _unitStateHighlights[unitInstanceId];
                Vector3 center = binding.UnitRoot.position;
                Vector3 worldTop = center + (xAxis + yAxis) * 0.5f;
                Vector3 worldRight = center + (xAxis - yAxis) * 0.5f;
                Vector3 worldBottom = center - (xAxis + yAxis) * 0.5f;
                Vector3 worldLeft = center - (xAxis - yAxis) * 0.5f;

                int vertexBase = highlightIndex * 4;
                _unitStateVertices[vertexBase] = _unitStateMeshObject.transform.InverseTransformPoint(worldTop);
                _unitStateVertices[vertexBase + 1] = _unitStateMeshObject.transform.InverseTransformPoint(worldRight);
                _unitStateVertices[vertexBase + 2] = _unitStateMeshObject.transform.InverseTransformPoint(worldBottom);
                _unitStateVertices[vertexBase + 3] = _unitStateMeshObject.transform.InverseTransformPoint(worldLeft);

                Color color = GetColorForType(binding.Type);
                _unitStateColors[vertexBase] = color;
                _unitStateColors[vertexBase + 1] = color;
                _unitStateColors[vertexBase + 2] = color;
                _unitStateColors[vertexBase + 3] = color;
                binding.LastWorldPosition = center;
                highlightIndex++;
            }

            _unitStateMesh.Clear();
            _unitStateMesh.vertices = _unitStateVertices;
            _unitStateMesh.colors = _unitStateColors;
            _unitStateMesh.triangles = _unitStateTriangles;
            _unitStateMesh.RecalculateBounds();
        }

        private void EnsureUnitStateBuffers(int highlightCount)
        {
            int vertexCount = highlightCount * 4;
            int triangleIndexCount = highlightCount * 6;
            if (_unitStateVertices.Length == vertexCount &&
                _unitStateColors.Length == vertexCount &&
                _unitStateTriangles.Length == triangleIndexCount)
            {
                return;
            }

            _unitStateVertices = new Vector3[vertexCount];
            _unitStateColors = new Color[vertexCount];
            _unitStateTriangles = new int[triangleIndexCount];
            for (int index = 0; index < highlightCount; index++)
            {
                int vertexBase = index * 4;
                int triangleBase = index * 6;
                _unitStateTriangles[triangleBase] = vertexBase;
                _unitStateTriangles[triangleBase + 1] = vertexBase + 1;
                _unitStateTriangles[triangleBase + 2] = vertexBase + 2;
                _unitStateTriangles[triangleBase + 3] = vertexBase;
                _unitStateTriangles[triangleBase + 4] = vertexBase + 2;
                _unitStateTriangles[triangleBase + 5] = vertexBase + 3;
            }
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

                    TilemapCellGeometry.GetDiamondVerticesWorld(
                        _gridLayer,
                        pos,
                        out Vector3 worldTop,
                        out Vector3 worldRight,
                        out Vector3 worldBottom,
                        out Vector3 worldLeft);

                    int vBase = cellIndex * 4;
                    int tBase = cellIndex * 6;

                    vertices[vBase + 0] = transform.InverseTransformPoint(worldTop);
                    vertices[vBase + 1] = transform.InverseTransformPoint(worldRight);
                    vertices[vBase + 2] = transform.InverseTransformPoint(worldBottom);
                    vertices[vBase + 3] = transform.InverseTransformPoint(worldLeft);

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

        private sealed class UnitStateHighlightBinding
        {
            internal UnitStateHighlightBinding(Transform unitRoot, TileHighlightType type)
            {
                UnitRoot = unitRoot;
                Type = type;
                LastWorldPosition = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            }

            internal Transform UnitRoot { get; }
            internal TileHighlightType Type { get; }
            internal Vector3 LastWorldPosition { get; set; }
        }
    }
}
