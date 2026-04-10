using Tactics.Tbsf.Common.Cells;
using Tactics.Tbsf.Common.Controllers;
using Tactics.Tbsf.Common.Units;
using Tactics.Tbsf.Common.Utilities;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;

namespace Tactics.Units
{
    public class TilemapUnit : Unit
    {
        [SerializeField] private Vector2Int _startingCellCoordinates;
        private ICell _currentCell;
        private bool _cellInitialized;
        [SerializeField] private UnityCellManager _cellManager;
        [SerializeField] private Tilemap _dataTilemap;

        private float baseMovementSpeed;

        public override ICell CurrentCell
        {
            get
            {
                if (!_cellInitialized)
                {
                    Vector3Int gridPos = _dataTilemap.WorldToCell(WorldPosition.ToVector3());
                    _currentCell = _cellManager.GetCellAt(new Vector2IntImpl(gridPos.x, gridPos.y));
                    _currentCell.IsTaken = true;
                    _currentCell.CurrentUnits.Add(this);
                    _cellInitialized = true;
                }
                return _currentCell;
            }
            set
            {
                _currentCell = value;
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
            base.Initialize(gridController);
            baseMovementSpeed = MovementAnimationSpeed;
            UnitLeftCell += OnUnitEnteredCell;
            UnitMoved += OnUnitMoved;

            WorldPosition = CurrentCell.WorldPosition;
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
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
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