using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Units;
using Tactics.Common.Utilities;
using Tactics.Runtime.Utilities;
using Tactics.Units;
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
        private InputAction _cellClickAction;

        private void Awake()
        {
            EnsureHighlightRenderer();
        }

        private void OnEnable()
        {
            _cellClickAction ??= new InputAction("CellClick", InputActionType.Button, "<Mouse>/leftButton");
            _cellClickAction.performed += OnCellClickPerformed;
            _cellClickAction.Enable();
        }

        private void OnDisable()
        {
            if (_cellClickAction == null)
                return;

            _cellClickAction.performed -= OnCellClickPerformed;
            _cellClickAction.Disable();
            _cellClickAction.Dispose();
            _cellClickAction = null;
        }

        /// <summary>
        /// Handles pointer presses through an Input Action callback so physical and virtual mouse
        /// events are consumed in the same Input System update that produced them.
        /// </summary>
        private void OnCellClickPerformed(InputAction.CallbackContext context)
        {
            if (_cells == null)
                return;

            var cell = TryGetCellUnderCursor();
            cell?.OnMouseDown();
        }

        private void EnsureHighlightRenderer()
        {
            _ = HighlightRenderer;
        }

        private VirtualSquareCell _selectedCell;
        private float _lastRaycast = 0;
        [SerializeField] private float _raycastDelay = 0.1f;
        private Vector3 _lastRawWorldPos;

        private ProceduralTileHighlightRenderer _highlightRenderer;

        public bool ShowDebugOverlay { get; set; }

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

                var worldPosition = TilemapCellGeometry.GetGroundCenterWorld(_gridLayer, pos).ToIVector3();
                var gridPosition = new Vector2IntImpl(pos.x, pos.y);
                var cell = new VirtualSquareCell(gridPosition, worldPosition, 1, false, null);
                _cells.Add(gridPosition, cell);
                CellAdded?.Invoke(cell);
            }

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
            if (_cells == null)
            {
                return null;
            }

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

        private void OnGUI()
        {
            if (!ShowDebugOverlay || _cells == null || _selectedCell == null) return;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = Color.white;

            float y = 10;
            float lineH = 22;

            var screenPos = Mouse.current.position.ReadValue();
            var cellCoords = _selectedCell.GridCoordinates;
            var raw = _lastRawWorldPos;
            var coordinates = new Vector3Int(cellCoords.x, cellCoords.y, 0);
            var origin = _gridLayer.CellToWorld(coordinates);
            var center = TilemapCellGeometry.GetGroundCenterWorld(_gridLayer, coordinates);
            float dx = raw.x - center.x;
            float dy = raw.y - center.y;

            GUI.Label(new Rect(10, y, 600, lineH), $"Cell: ({cellCoords.x},{cellCoords.y})", style); y += lineH;
            GUI.Label(new Rect(10, y, 600, lineH), $"GroundCenter: ({center.x:F2}, {center.y:F2})", style); y += lineH;
            GUI.Label(new Rect(10, y, 600, lineH), $"MouseWorld: ({raw.x:F2}, {raw.y:F2})", style); y += lineH;

            style.normal.textColor = Mathf.Abs(dy) > 0.1f ? Color.yellow : Color.green;
            GUI.Label(new Rect(10, y, 600, lineH), $"Delta: ({dx:F3}, {dy:F3})  [Y offset = {dy / 0.5f:F2} tileH]", style); y += lineH;

            style.normal.textColor = Color.cyan;
            GUI.Label(new Rect(10, y, 600, lineH), $"Screen: ({screenPos.x:F0}, {screenPos.y:F0})", style); y += lineH;

            style.normal.textColor = Color.white;
            GUI.Label(new Rect(10, y, 600, lineH), $"CellOrigin: ({origin.x:F2}, {origin.y:F2})", style); y += lineH;

            var screenCenter = _mainCamera.WorldToScreenPoint(center);
            var screenCellOrigin = _mainCamera.WorldToScreenPoint(origin);
            style.normal.textColor = Color.green;
            GUI.Label(new Rect(10, y, 600, lineH), $"Center→Screen: ({screenCenter.x:F0}, {screenCenter.y:F0})", style); y += lineH;
            style.normal.textColor = Color.magenta;
            GUI.Label(new Rect(10, y, 600, lineH), $"CellOrigin→Screen: ({screenCellOrigin.x:F0}, {screenCellOrigin.y:F0})", style); y += lineH;

            screenCenter.y = Screen.height - screenCenter.y;
            DrawCrosshair(screenCenter, Color.green, 12);

            screenCellOrigin.y = Screen.height - screenCellOrigin.y;
            DrawCrosshair(screenCellOrigin, Color.magenta, 8);

            var mouseScr = screenPos;
            mouseScr.y = Screen.height - mouseScr.y;
            DrawCrosshair(mouseScr, Color.red, 10);

            DrawUnitDebug(style, y);
        }

        private void DrawUnitDebug(GUIStyle style, float startY)
        {
            var units = FindObjectsByType<Tactics.Common.Units.Unit>(FindObjectsSortMode.None);
            if (units.Length == 0) return;

            float y = startY + 10;
            float lineH = 20;
            var smallStyle = new GUIStyle(style) { fontSize = 13 };

            foreach (var u in units)
            {
                var sr = u.GetComponentInChildren<SpriteRenderer>();
                if (sr == null || sr.sprite == null) continue;

                var srPos = sr.transform.position;
                var bounds = sr.sprite.bounds;
                var bottomCenter = srPos + new Vector3(0, bounds.min.y, 0);
                var cellPos = TilemapCellGeometry.WorldToCell(_gridLayer, u.transform.position);
                var cellOrigin = _gridLayer.CellToWorld(cellPos);
                var cellCenter = TilemapCellGeometry.GetGroundCenterWorld(_gridLayer, cellPos);

                smallStyle.normal.textColor = Color.yellow;
                GUI.Label(new Rect(Screen.width - 350, y, 340, lineH), $"{u.name}:", smallStyle); y += lineH;
                smallStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(Screen.width - 350, y, 340, lineH), $"  spriteBottom=({bottomCenter.x:F2},{bottomCenter.y:F2})", smallStyle); y += lineH;
                smallStyle.normal.textColor = Color.magenta;
                GUI.Label(new Rect(Screen.width - 350, y, 340, lineH), $"  cellOrigin=({cellOrigin.x:F2},{cellOrigin.y:F2})", smallStyle); y += lineH;
                smallStyle.normal.textColor = Color.green;
                GUI.Label(new Rect(Screen.width - 350, y, 340, lineH), $"  groundCenter=({cellCenter.x:F2},{cellCenter.y:F2})", smallStyle); y += lineH;

                float deltaBottom = bottomCenter.y - cellCenter.y;
                smallStyle.normal.textColor = Mathf.Abs(deltaBottom) < 0.05f ? Color.green : Color.red;
                GUI.Label(new Rect(Screen.width - 350, y, 340, lineH), $"  bottom→center delta={deltaBottom:F3}", smallStyle); y += lineH;

                var scrBottom = _mainCamera.WorldToScreenPoint(bottomCenter);
                scrBottom.y = Screen.height - scrBottom.y;
                DrawCrosshair(scrBottom, Color.yellow, 8);

                var scrCellCenter = _mainCamera.WorldToScreenPoint(cellCenter);
                scrCellCenter.y = Screen.height - scrCellCenter.y;
                DrawCrosshair(scrCellCenter, Color.magenta, 8);

                y += 5;
            }
        }

        private void DrawCrosshair(Vector2 screenPos, Color color, float size)
        {
            var prevColor = GUI.color;
            GUI.color = color;
            var tex = Texture2D.whiteTexture;
            GUI.DrawTexture(new Rect(screenPos.x - size, screenPos.y - 1, size * 2, 3), tex);
            GUI.DrawTexture(new Rect(screenPos.x - 1, screenPos.y - size, 3, size * 2), tex);
            GUI.color = prevColor;
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
            _lastRawWorldPos = mouseWorldPos;
            Vector3Int cellPos = TilemapCellGeometry.WorldToCell(_gridLayer, mouseWorldPos);

            var gridPosition = new Vector2IntImpl(cellPos.x, cellPos.y);

            if (!_cells.TryGetValue(gridPosition, out var cell))
            {
                return null;
            }

            Vector3 cellWorldCenter = TilemapCellGeometry.GetGroundCenterWorld(_gridLayer, cellPos);

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

        public override Task MarkAsGuidance(IEnumerable<ICell> cells, CellGuidanceType guidanceType)
        {
            HighlightRenderer?.SetHighlights(cells, ToTileHighlightType(guidanceType));
            return Task.CompletedTask;
        }

        public override Task UnMarkGuidance(IEnumerable<ICell> cells, CellGuidanceType guidanceType)
        {
            HighlightRenderer?.RemoveHighlightsOfType(cells, ToTileHighlightType(guidanceType));
            return Task.CompletedTask;
        }

        /// <summary>
        /// Adds a persistent unit-state overlay to a single isometric cell.
        /// Unit states use their own mesh layer so they never fall back to the legacy square Sprite marker.
        /// </summary>
        public void AddUnitStateHighlight(ICell cell, TileHighlightType type)
        {
            HighlightRenderer?.AddHighlights(new[] { cell }, type);
        }

        /// <summary>
        /// Removes a persistent unit-state overlay without clearing ability range or hover highlights.
        /// </summary>
        public void RemoveUnitStateHighlight(ICell cell, TileHighlightType type)
        {
            if (cell == null || _highlightRenderer == null)
                return;

            // Scene teardown may destroy the renderer before units clear their state.
            // Removal is best-effort and must not lazily recreate rendering components.
            _highlightRenderer.RemoveHighlightOfType(cell, type);
        }

        internal void SetUnitStateHighlight(TilemapUnit unit, TileHighlightType type)
        {
            if (unit == null)
                return;

            HighlightRenderer?.SetUnitStateHighlight(unit.GetInstanceID(), unit.transform, type);
        }

        internal void RemoveUnitStateHighlight(TilemapUnit unit)
        {
            if (unit == null || _highlightRenderer == null)
                return;

            // Unit teardown must not recreate a renderer that the scene has already destroyed.
            _highlightRenderer.RemoveUnitStateHighlight(unit.GetInstanceID());
        }

        private static TileHighlightType ToTileHighlightType(CellGuidanceType guidanceType)
        {
            return guidanceType switch
            {
                CellGuidanceType.SpearLocation => TileHighlightType.SpearLocation,
                CellGuidanceType.SpearPickup => TileHighlightType.SpearPickup,
                _ => throw new ArgumentOutOfRangeException(nameof(guidanceType), guidanceType, null)
            };
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
