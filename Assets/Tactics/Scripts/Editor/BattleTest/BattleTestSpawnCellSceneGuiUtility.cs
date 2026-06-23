using Tactics.Common.Cells;
using Tactics.Common.Utilities;
using UnityEditor;
using UnityEngine;

namespace Tactics.Editor.BattleTest
{
    public static class BattleTestSpawnCellSceneGuiUtility
    {
        public static readonly Color PartyColor = new(0.3f, 0.9f, 0.4f, 1f);
        public static readonly Color EnemyColor = new(0.95f, 0.35f, 0.25f, 1f);
        public static readonly Color CorpseColor = new(0.7f, 0.45f, 0.9f, 1f);
        public static readonly Color SelectedTint = new(1f, 1f, 0.2f, 1f);

        private static ICellManager _cachedCellManager;
        private static Grid _cachedGrid;
        private static Vector3 _anchorOffset;
        private static bool _anchorCached;

        public static void SetCellManager(ICellManager cellManager)
        {
            _cachedCellManager = cellManager;
        }

        public static Grid GetGrid()
        {
            if (_cachedGrid != null)
                return _cachedGrid;

            _cachedGrid = Object.FindFirstObjectByType<Grid>();
            _anchorCached = false;
            return _cachedGrid;
        }

        public static void InvalidateCache()
        {
            _cachedCellManager = null;
            _cachedGrid = null;
            _anchorCached = false;
        }

        private static Vector3 GetAnchorOffset(Grid grid)
        {
            if (!_anchorCached || _cachedGrid != grid)
            {
                _anchorOffset = grid.GetCellCenterWorld(Vector3Int.zero)
                              - grid.CellToWorld(Vector3Int.zero);
                _anchorCached = true;
                _cachedGrid = grid;
            }
            return _anchorOffset;
        }

        public static bool TryGetCellWorldPosition(Vector2Int cell, out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;

            if (_cachedCellManager != null)
            {
                var icell = _cachedCellManager.GetCellAt(cell.ToIVector2Int());
                if (icell != null)
                {
                    worldPosition = icell.WorldPosition.ToVector3();
                    return true;
                }
            }

            var grid = GetGrid();
            if (grid != null)
            {
                worldPosition = grid.CellToWorld(new Vector3Int(cell.x, cell.y, 0))
                              + GetAnchorOffset(grid);
                return true;
            }

            return false;
        }

        private static UnityEngine.Tilemaps.Tilemap _cachedTilemap;

        private static bool HasTile(Grid grid, Vector3Int cellPos)
        {
            if (_cachedTilemap == null)
                _cachedTilemap = grid.GetComponentInChildren<UnityEngine.Tilemaps.Tilemap>();
            return _cachedTilemap != null && _cachedTilemap.HasTile(cellPos);
        }

        public static bool TryGetSnappedCell(Vector3 worldPosition, out Vector2Int cell)
        {
            cell = default;

            if (_cachedCellManager != null)
            {
                var grid = GetGrid();
                if (grid != null)
                {
                    var cellPos = grid.WorldToCell(worldPosition);
                    var icell = _cachedCellManager.GetCellAt(new Vector2IntImpl(cellPos.x, cellPos.y));
                    if (icell != null)
                    {
                        cell = new Vector2Int(cellPos.x, cellPos.y);
                        return true;
                    }
                }
            }

            var gridOnly = GetGrid();
            if (gridOnly != null)
            {
                var cellPos = gridOnly.WorldToCell(worldPosition);
                if (HasTile(gridOnly, cellPos))
                {
                    cell = new Vector2Int(cellPos.x, cellPos.y);
                    return true;
                }
                return false;
            }

            return false;
        }

        public static bool DrawInteractiveHandle(
            Vector2Int cell,
            Color color,
            string label,
            bool selected,
            out Vector2Int newCell,
            out bool clicked)
        {
            newCell = cell;
            clicked = false;

            if (!TryGetCellWorldPosition(cell, out var worldPos))
                return false;

            float baseSize = HandleUtility.GetHandleSize(worldPos);
            float handleSize = selected ? baseSize * 0.25f : baseSize * 0.18f;

            Handles.color = selected ? SelectedTint : color;

            if (selected)
            {
                EditorGUI.BeginChangeCheck();
                var newPos = Handles.FreeMoveHandle(worldPos, handleSize, Vector3.one * 0.5f, Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    if (TryGetSnappedCell(newPos, out var snapped))
                    {
                        newCell = snapped;
                        return true;
                    }
                }
            }
            else
            {
                var controlId = GUIUtility.GetControlID(FocusType.Passive);
                Handles.SphereHandleCap(controlId, worldPos, Quaternion.identity, handleSize, EventType.Repaint);

                if (Event.current.type == EventType.MouseDown
                    && Event.current.button == 0
                    && !Event.current.alt
                    && HandleUtility.nearestControl == controlId)
                {
                    clicked = true;
                    Event.current.Use();
                }
            }

            if (!string.IsNullOrEmpty(label))
            {
                var style = new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = selected ? SelectedTint : color },
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 10
                };
                Handles.Label(worldPos + Vector3.up * (handleSize + 0.15f), label, style);
            }

            return false;
        }
    }
}
