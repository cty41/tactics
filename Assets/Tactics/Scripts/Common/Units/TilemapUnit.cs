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
        [SerializeField] private Vector2Int _startingCellCoordinates;
        private ICell _currentCell;
        private bool _cellInitialized;
        [SerializeField] private UnityCellManager _cellManager;
        [FormerlySerializedAs("_dataTilemap")]
        [SerializeField] private Tilemap _gridTilemap;
        [SerializeField, Tooltip("垂直偏移量，用于将单位视觉居中于 tile 中心")]
        private float _visualYOffset = 0.25f;

        private float baseMovementSpeed;

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
                    Vector3Int gridPos = _gridTilemap.WorldToCell(WorldPosition.ToVector3());
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
            UnitLeftCell += OnUnitEnteredCell;
            UnitMoved += OnUnitMoved;

            var cell = CurrentCell;
            if (cell != null)
            {
                WorldPosition = cell.WorldPosition;
                TLog.Info($"[TilemapUnit] Initialize {gameObject.name}: cellGrid=({cell.GridCoordinates.x},{cell.GridCoordinates.y}), cellWorldPos=({cell.WorldPosition.x:F2},{cell.WorldPosition.y:F2},{cell.WorldPosition.z:F2}), unitWorldPos=({WorldPosition.x:F2},{WorldPosition.y:F2},{WorldPosition.z:F2}), cellSize=({_gridTilemap.layoutGrid.cellSize.x:F2},{_gridTilemap.layoutGrid.cellSize.y:F2})");
                ApplyVisualYOffset();
            }
            else
                TLog.Warning($"[{nameof(TilemapUnit)}] CurrentCell is null during Initialize for {gameObject.name}.");
        }

        private void ApplyVisualYOffset()
        {
            if (Mathf.Approximately(_visualYOffset, 0f)) return;

            var spriteRenderers = GetComponentsInChildren<SpriteRenderer>(false);
            SpriteRenderer mainSr = null;
            SpriteRenderer shadowSr = null;
            float maxArea = 0f;
            foreach (var sr in spriteRenderers)
            {
                if (sr.sprite == null) continue;
                if (sr.name == "Shadow") shadowSr = sr;
                var area = sr.sprite.rect.width * sr.sprite.rect.height;
                if (area > maxArea)
                {
                    maxArea = area;
                    mainSr = sr;
                }
            }

            if (mainSr != null && mainSr.transform != transform)
            {
                var localPos = mainSr.transform.localPosition;
                localPos.y += _visualYOffset;
                mainSr.transform.localPosition = localPos;

                if (shadowSr != null && shadowSr != mainSr)
                {
                    var shadowPos = shadowSr.transform.localPosition;
                    shadowPos.y = localPos.y + mainSr.sprite.bounds.min.y;
                    shadowSr.transform.localPosition = shadowPos;
                }
            }
        }

        private void OnUnitMoved(UnitMovedEventArgs obj)
        {
            MovementAnimationSpeed = baseMovementSpeed;
        }

        private void OnUnitEnteredCell(UnitChangedGridPositionEventArgs obj)
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