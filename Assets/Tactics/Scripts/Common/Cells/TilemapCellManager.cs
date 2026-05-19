using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Units;
using Tactics.Common.Utilities;
using Tactics.Runtime.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

namespace Tactics.Cells
{
    public class TilemapCellManager : UnityCellManager
    {
        [SerializeField] private Camera _mainCamera;

        public override event Action<ICell> CellAdded;
#pragma warning disable CS0067 // No dynamic cell removal in this manager
        public override event Action<ICell> CellRemoved;
#pragma warning restore CS0067

        [FormerlySerializedAs("_dataLayer")]
        [SerializeField] Tilemap _gridLayer;
        [SerializeField] Tilemap _obstacleLayer;

        HashSet<Vector2IntImpl> _blockedCells;

        [SerializeField] private Tile _lineHorizontal;
        [SerializeField] private Tile _lineVertical;
        [SerializeField] private Tile _curlLowerRight;
        [SerializeField] private Tile _curlUpperLeft;
        [SerializeField] private Tile _curlLowerLeft;
        [SerializeField] private Tile _curlUpperRight;
        [SerializeField] private Tile _arrowLeft;
        [SerializeField] private Tile _arrowRight;
        [SerializeField] private Tile _arrowUp;
        [SerializeField] private Tile _arrowDown;

        Dictionary<Vector2IntImpl, VirtualSquareCell> _cells;

        private void Awake()
        {
            EnsureHighlightRenderer();
        }

        private void EnsureHighlightRenderer()
        {
            _ = HighlightRenderer;
        }

        private VirtualSquareCell _selectedCell;
        private float _lastRaycast = 0;
        [SerializeField] private float _raycastDelay = 0.1f;

        private ProceduralTileHighlightRenderer _highlightRenderer;

        private ProceduralTileHighlightRenderer HighlightRenderer
        {
            get
            {
                if (_highlightRenderer == null)
                {
                    _highlightRenderer = GetComponent<ProceduralTileHighlightRenderer>();
                    if (_highlightRenderer == null)
                    {
                        _highlightRenderer = gameObject.AddComponent<ProceduralTileHighlightRenderer>();
                    }
                }
                return _highlightRenderer;
            }
        }

        public override void Initialize(IGridController gridController)
        {
            EnsureHighlightRenderer();
            HighlightRenderer.SetDataLayer(_gridLayer);

            var bounds = _gridLayer.cellBounds;
            _cells = new Dictionary<Vector2IntImpl, VirtualSquareCell>();

            foreach (Vector3Int pos in bounds.allPositionsWithin)
            {
                var tile = _gridLayer.GetTile(pos);
                if (tile == null)
                    continue;

                var worldPosition = _gridLayer.GetCellCenterWorld(pos).ToIVector3();
                var gridPosition = new Vector2IntImpl(pos.x, pos.y);
                var cell = new VirtualSquareCell(gridPosition, worldPosition, 1, false, null);
                _cells.Add(gridPosition, cell);
                CellAdded?.Invoke(cell);
            }

            // 诊断日志：验证 GetCellCenterWorld 与 WorldToCell 往返一致性
            var firstCellData = _cells.Values.First();
            var fcGridPos = new Vector3Int(firstCellData.GridCoordinates.x, firstCellData.GridCoordinates.y, 0);
            var center = _gridLayer.GetCellCenterWorld(fcGridPos);
            var rt = _gridLayer.WorldToCell(center);
            TLog.Info($"[TilemapCellManager] Initialize: firstCell gridCoord=({fcGridPos.x},{fcGridPos.y}), centerWorld={center:F2}, roundTrip=({rt.x},{rt.y})");

            if (_cells.Count == 0)
            {
                throw new InvalidOperationException(
                    $"TilemapCellManager.Initialize: No cells found in {_gridLayer.name}. " +
                    $"Bounds: {bounds}. Make sure the Grid Layer tilemap contains tiles within the desired play area."
                );
            }

            _selectedCell = _cells.Values.First();
            BuildBlockedCells();
        }

        public Tilemap GridLayer => _gridLayer;

        public override ICell GetCellAt(Vector2IntImpl coords)
        {
            if (_cells.TryGetValue(coords, out var cell))
            {
                return cell;
            }
            return null;
        }

        public override bool IsCellWalkable(ICell cell)
        {
            return cell != null && !_blockedCells.Contains(cell.GridCoordinates);
        }

        public void RebuildBlockedCells()
        {
            _blockedCells?.Clear();
            BuildBlockedCells();
        }

        private void BuildBlockedCells()
        {
            if (_obstacleLayer == null)
            {
                _blockedCells = new HashSet<Vector2IntImpl>();
                return;
            }

            _blockedCells = new HashSet<Vector2IntImpl>();

            foreach (var kvp in _cells)
            {
                var worldPos = kvp.Value.WorldPosition.ToVector3();
                var colliders = Physics2D.OverlapPointAll(worldPos);
                if (colliders.Any(c => !c.isTrigger))
                {
                    _blockedCells.Add(kvp.Key);
                }
            }
        }

        void Update()
        {
            if (_cells == null) return; // 尚未初始化，跳过
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                var cell = TryGetCellUnderCursor();
                cell?.OnMouseDown();
            }

            var currentTime = Time.time;
            if (currentTime - _lastRaycast < _raycastDelay)
            {
                return;
            }

            _lastRaycast = currentTime;

            var highlightedCell = TryGetCellUnderCursor();
            if (_selectedCell != highlightedCell)
            {
                _selectedCell?.OnMouseExit();
                highlightedCell?.OnMouseEnter();
                _selectedCell = highlightedCell;
            }
        }

        private VirtualSquareCell TryGetCellUnderCursor()
        {
            if (_cells == null) return null; // 尚未初始化
            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            
            // Use Plane.Raycast to get accurate world position on the grid plane
            // This works correctly for isometric cameras where ScreenToWorldPoint with z=0 fails
            float gridZ = _gridLayer.transform.position.z;
            Plane gridPlane = new Plane(Vector3.back, new Vector3(0, 0, gridZ));
            Ray ray = _mainCamera.ScreenPointToRay(mouseScreenPos);
            
            if (!gridPlane.Raycast(ray, out float enter))
            {
                return null;
            }
            
            Vector3 mouseWorldPos = ray.GetPoint(enter);
            Vector3Int cellPos = _gridLayer.WorldToCell(mouseWorldPos);
            TLog.Info($"[TilemapCellManager] TryGetCellUnderCursor: mouseWorldPos={mouseWorldPos:F2}, cellPos=({cellPos.x},{cellPos.y})");

            var gridPosition = new Vector2IntImpl(cellPos.x, cellPos.y);

            if (!_cells.TryGetValue(gridPosition, out var cell))
            {
                return null;
            }

            Vector3 cellWorldCenter = _gridLayer.GetCellCenterWorld(cellPos);

            Collider2D[] colliders2D = Physics2D.OverlapPointAll(cellWorldCenter);
            var blocking2D = colliders2D.Where(c => !c.isTrigger && c.GetComponent<Unit>() == null).ToArray();
            if (blocking2D.Length > 0)
            {
                return null;
            }

            float checkRadius = 0.1f;
            Collider[] colliders3D = Physics.OverlapSphere(cellWorldCenter, checkRadius);
            var blocking3D = colliders3D.Where(c => !c.isTrigger && c.GetComponent<Unit>() == null).ToArray();
            if (blocking3D.Length > 0)
            {
                return null;
            }

            return cell;
        }

        public override IEnumerable<ICell> GetCells()
        {
            return _cells.Values;
        }

        public override Task MarkAsReachable(IEnumerable<ICell> cells)
        {
            HighlightRenderer?.AddHighlights(cells, TileHighlightType.Reachable);
            return Task.CompletedTask;
        }

        public override Task MarkAsReachable(ICell cell)
        {
            HighlightRenderer?.AddHighlights(new[] { cell }, TileHighlightType.Reachable);
            return Task.CompletedTask;
        }

        public override Task MarkAsHighlighted(ICell cell)
        {
            HighlightRenderer?.SetHighlights(new[] { cell }, TileHighlightType.Highlighted);
            return Task.CompletedTask;
        }

        public override Task UnMarkAsHighlighted(ICell cell)
        {
            HighlightRenderer?.RemoveHighlightOfType(cell, TileHighlightType.Highlighted);
            return Task.CompletedTask;
        }

        public override Task UnMark(IEnumerable<ICell> cells)
        {
            HighlightRenderer?.RemoveHighlights(cells);
            return Task.CompletedTask;
        }

        public override Task UnMark(ICell cell)
        {
            HighlightRenderer?.RemoveHighlight(cell);
            return Task.CompletedTask;
        }

        public override Task MarkAsPath(IEnumerable<ICell> cells, ICell originCell)
        {
            HighlightRenderer?.SetPathHighlights(cells);
            return Task.CompletedTask;
        }

        public override Task MarkAsAoE(IEnumerable<ICell> cells)
        {
            HighlightRenderer?.SetAoEHighlights(cells);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Determines the appropriate arrow segment sprite to use for a given segment of the path based on the relative positions of the previous, current, and next cells.
        /// </summary>
        /// <param name="origin">The origin point of the previous cell.</param>
        /// <param name="first">The current cell position.</param>
        /// <param name="second">The next cell position.</param>
        /// <returns>The sprite corresponding to the correct directional segment of the path.</returns>
        private Tile GetArrowSegmentSprite(Vector2IntImpl origin, Vector2IntImpl first, Vector2IntImpl second)
        {
            if (second.y == origin.y)
                return _lineHorizontal;
            if (second.x == origin.x)
                return _lineVertical;

            if (origin.y > second.y)
            {
                return origin.x > second.x
                    ? (origin.y != first.y ? _curlLowerRight : _curlUpperLeft)
                    : (origin.y != first.y ? _curlLowerLeft : _curlUpperRight);
            }
            else
            {
                return origin.x > second.x
                    ? (origin.y != first.y ? _curlUpperRight : _curlLowerLeft)
                    : (origin.y != first.y ? _curlUpperLeft : _curlLowerRight);
            }
        }
        /// <summary>
        /// Determines the appropriate arrowhead sprite to use at the end of the path based on the direction between the last two cells.
        /// </summary>
        /// <param name="from">The position of the second-to-last cell in the path.</param>
        /// <param name="to">The position of the last cell in the path.</param>
        /// <returns>The sprite corresponding to the correct arrowhead direction.</returns>
        private Tile GetArrowHeadSprite(Vector2IntImpl from, Vector2IntImpl to)
        {
            if (to.x != from.x)
                return to.x < from.x ? _arrowLeft : _arrowRight;
            return to.y <= from.y ? _arrowDown : _arrowUp;
        }

        public void SetReachableMovementMode(bool isMovement)
        {
            HighlightRenderer?.SetReachableMovementMode(isMovement);
        }

        public override void SetColor(ICell cell, float r, float g, float b, float a)
        {
        }


    }
}