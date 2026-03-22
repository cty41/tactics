using TurnBasedStrategyFramework.Common.Cells;
using TurnBasedStrategyFramework.Common.Controllers;
using TurnBasedStrategyFramework.Common.Units;
using TurnBasedStrategyFramework.Common.Utilities;
using TurnBasedStrategyFramework.Unity.Cells;
using TurnBasedStrategyFramework.Unity.Units;
using TurnBasedStrategyFramework.Unity.Utilities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;

namespace TurnBasedStrategyFramework.Unity.Examples.TilemapExample.Units
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
    }
}