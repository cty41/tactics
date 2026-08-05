using System.Threading.Tasks;
using Tactics.Cells;
using Tactics.Runtime.Utilities;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Units;
using Tactics.Common.Utilities;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

namespace Tactics.Units
{
    public class TilemapUnit : Unit
    {
        private const int FriendlyPlayerNumber = 0;

        [SerializeField] private Vector2Int _startingCellCoordinates;
        private ICell _currentCell;
        private bool _cellInitialized;
        [SerializeField] private UnityCellManager _cellManager;
        [FormerlySerializedAs("_dataTilemap")]
        [SerializeField] private Tilemap _gridTilemap;
        private float baseMovementSpeed;
        private bool _hasUnitStateHighlight;

        private void Awake()
        {
            EnsureTilemapRefs();
        }

        private void EnsureTilemapRefs()
        {
            if (_cellManager == null)
            {
                _cellManager = Object.FindFirstObjectByType<UnityCellManager>(
                    FindObjectsInactive.Include);
#pragma warning disable CS0618
                if (_cellManager == null)
                {
                    var managers = FindObjectsOfType<UnityCellManager>(true);
                    if (managers.Length > 0)
                        _cellManager = managers[0];
                }
#pragma warning restore CS0618
            }

            if (_gridTilemap == null)
            {
                var tcm = Object.FindFirstObjectByType<TilemapCellManager>(
                    FindObjectsInactive.Include);
                if (tcm != null)
                    _gridTilemap = tcm.GridLayer;
            }
        }

        public override ICell CurrentCell
        {
            get
            {
                if (!_cellInitialized)
                {
                    EnsureTilemapRefs();
                    if (_gridTilemap == null)
                    {
                        TLog.Error($"[{nameof(TilemapUnit)}] _gridTilemap is null on {gameObject.name}. Cannot initialize CurrentCell.");
                        return null;
                    }
                    if (_cellManager == null)
                    {
                        TLog.Error($"[{nameof(TilemapUnit)}] _cellManager is null on {gameObject.name}. Cannot initialize CurrentCell.");
                        return null;
                    }
                    Vector3Int gridPos = TilemapCellGeometry.WorldToCell(_gridTilemap, WorldPosition.ToVector3());
                    _currentCell = _cellManager.GetCellAt(new Vector2IntImpl(gridPos.x, gridPos.y));
                    if (_currentCell == null)
                    {
                        TLog.Error($"[{nameof(TilemapUnit)}] GetCellAt returned null for gridPos {gridPos} on {gameObject.name}.");
                        return null;
                    }
                    _currentCell.IsTaken = true;
                    _currentCell.CurrentUnits.Add(this);
                    _cellInitialized = true;
                }
                return _currentCell;
            }
            set
            {
                _currentCell = value;
                _cellInitialized = true;
            }
        }

        public override bool IsCellMovableTo(ICell cell)
        {
            return GetComponent<IMovementRules>().IsCellMovableTo(this, cell);
        }

        public override bool IsCellTraversable(ICell source, ICell destination)
        {
            return GetComponent<IMovementRules>().IsCellTraversable(this, source, destination);
        }

        public override float GetMovementCost(ICell source, ICell destination)
        {
            return GetComponent<IMovementRules>().GetMovementCost(this, source, destination);
        }


        public override void Initialize(IGridController gridController)
        {
            EnsureTilemapRefs();
            base.Initialize(gridController);
            baseMovementSpeed = MovementAnimationSpeed;
            UnitLeftCell += OnUnitLeavingCell;
            UnitMoved += OnUnitMoved;

            var cell = CurrentCell;
            if (cell != null)
            {
                WorldPosition = cell.WorldPosition;
                TLog.Info($"[TilemapUnit] Initialize {gameObject.name}: cellGrid=({cell.GridCoordinates.x},{cell.GridCoordinates.y}), cellWorldPos=({cell.WorldPosition.x:F2},{cell.WorldPosition.y:F2},{cell.WorldPosition.z:F2}), unitWorldPos=({WorldPosition.x:F2},{WorldPosition.y:F2},{WorldPosition.z:F2}), cellSize=({_gridTilemap.layoutGrid.cellSize.x:F2},{_gridTilemap.layoutGrid.cellSize.y:F2})");
            }
            else
                TLog.Warning($"[{nameof(TilemapUnit)}] CurrentCell is null during Initialize for {gameObject.name}.");
        }

        public override Task UnMark()
        {
            ClearUnitStateHighlight();
            return base.UnMark();
        }

        public override Task MarkAsSelected()
        {
            SetUnitStateHighlight(TileHighlightType.UnitSelected);
            return base.MarkAsSelected();
        }

        public override Task MarkAsFriendly()
        {
            SetUnitStateHighlight(TileHighlightType.UnitFriendly);
            return base.MarkAsFriendly();
        }

        public override Task MarkAsFinished()
        {
            SetUnitStateHighlight(TileHighlightType.UnitFinished);
            return base.MarkAsFinished();
        }

        public override Task MarkAsTargetable()
        {
            SetUnitStateHighlight(TileHighlightType.UnitTargetable);
            return base.MarkAsTargetable();
        }

        private void SetUnitStateHighlight(TileHighlightType type)
        {
            ClearUnitStateHighlight();
            var tilemapCellManager = _cellManager as TilemapCellManager;
            if (tilemapCellManager == null || !ShouldDisplayUnitStateHighlight(type))
                return;

            tilemapCellManager.SetUnitStateHighlight(this, type);
            _hasUnitStateHighlight = true;
        }

        private void ClearUnitStateHighlight()
        {
            if (_hasUnitStateHighlight)
            {
                var tilemapCellManager = _cellManager as TilemapCellManager;
                if (tilemapCellManager != null)
                    tilemapCellManager.RemoveUnitStateHighlight(this);
            }

            _hasUnitStateHighlight = false;
        }

        private bool ShouldDisplayUnitStateHighlight(TileHighlightType type)
        {
            return PlayerNumber == FriendlyPlayerNumber || type == TileHighlightType.UnitTargetable;
        }

        public override void OnDestroyed(IGridController gridController)
        {
            ClearUnitStateHighlight();
            base.OnDestroyed(gridController);
        }

        private void OnDisable()
        {
            ClearUnitStateHighlight();
        }

        protected override void OnDestroy()
        {
            ClearUnitStateHighlight();
            base.OnDestroy();
        }

        private void OnUnitMoved(UnitMovedEventArgs obj)
        {
            MovementAnimationSpeed = baseMovementSpeed;
        }

        private void OnUnitLeavingCell(UnitChangedGridPositionEventArgs obj)
        {
            MovementAnimationSpeed = baseMovementSpeed / obj.EnteredCell.MovementCost;
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            TLog.Info($"[TilemapUnit] OnPointerEnter: {gameObject.name}, CurrentCell={CurrentCell?.GridCoordinates}");
            if (CurrentCell != null)
                CurrentCell.InvokeCellHighlighted();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            TLog.Info($"[TilemapUnit] OnPointerExit: {gameObject.name}, CurrentCell={CurrentCell?.GridCoordinates}");
            if (CurrentCell != null)
                CurrentCell.InvokeCellDehighlighted();
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            base.OnPointerClick(eventData);
        }

        [FoldoutGroup("Derived Stats (Resources)"), ShowInInspector, ReadOnly, PropertyOrder(100), LabelText("Derived Max Health")]
        private float PreviewMaxHealth => Mathf.Max(1f, Constitution * 4f);

        [FoldoutGroup("Derived Stats (Resources)"), ShowInInspector, ReadOnly, PropertyOrder(101), LabelText("Derived Max Mana")]
        private float PreviewMaxMana => Mathf.Max(0f, Charisma * 3f);

        [FoldoutGroup("Derived Stats (Resources)"), ShowInInspector, ReadOnly, PropertyOrder(102), LabelText("Derived Max Movement Points")]
        private float PreviewMaxMovementPoints => Mathf.Max(1f, Speed);

        [FoldoutGroup("Derived Stats (Resources)"), ShowInInspector, ReadOnly, PropertyOrder(103), LabelText("Mana Regen per Turn (End)")]
        private int PreviewManaRegenPerTurn => Mathf.Max(0, Intelligence);

        [FoldoutGroup("Derived Stats (Combat)"), ShowInInspector, ReadOnly, PropertyOrder(110), LabelText("Melee Damage (Non-Crit)")]
        private float PreviewMeleeBaseDamage => CombatComponent.CalculateBaseDamageBeforeCrit(this, false);

        [FoldoutGroup("Derived Stats (Combat)"), ShowInInspector, ReadOnly, PropertyOrder(111), LabelText("Ranged Damage (Non-Crit)")]
        private float PreviewRangedBaseDamage => CombatComponent.CalculateBaseDamageBeforeCrit(this, true);

        [FoldoutGroup("Derived Stats (Combat)"), ShowInInspector, ReadOnly, PropertyOrder(112), LabelText("Crit Chance"), SuffixLabel("%")]
        private float PreviewCritChancePercent => CombatComponent.GetClampedCritChance(this) * 100f;

        [FoldoutGroup("Derived Stats (Combat)"), ShowInInspector, ReadOnly, PropertyOrder(113), LabelText("Melee Crit Damage")]
        private float PreviewMeleeCriticalDamage => CombatComponent.GetCriticalDamage(PreviewMeleeBaseDamage);

        [FoldoutGroup("Derived Stats (Combat)"), ShowInInspector, ReadOnly, PropertyOrder(114), LabelText("Ranged Crit Damage")]
        private float PreviewRangedCriticalDamage => CombatComponent.GetCriticalDamage(PreviewRangedBaseDamage);

        [FoldoutGroup("Derived Stats (Combat)"), ShowInInspector, ReadOnly, PropertyOrder(115), LabelText("Melee Expected Damage")]
        private float PreviewMeleeExpectedDamage => CombatComponent.GetExpectedDamage(PreviewMeleeBaseDamage, CombatComponent.GetClampedCritChance(this));

        [FoldoutGroup("Derived Stats (Combat)"), ShowInInspector, ReadOnly, PropertyOrder(116), LabelText("Ranged Expected Damage")]
        private float PreviewRangedExpectedDamage => CombatComponent.GetExpectedDamage(PreviewRangedBaseDamage, CombatComponent.GetClampedCritChance(this));

        [FoldoutGroup("Derived Stats (Combat)"), ShowInInspector, ReadOnly, PropertyOrder(117), LabelText("Defence Factor")]
        private int PreviewDefenceFactor => DefenceFactor;

        [FoldoutGroup("Derived Stats (Combat)"), ShowInInspector, ReadOnly, PropertyOrder(118), LabelText("Attack Range")]
        private int PreviewAttackRange => AttackRange;
    }
}
