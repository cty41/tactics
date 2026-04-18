using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tactics.Common.AI.BehaviourTrees;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using Tactics.Common.Units.Buffs;
using Tactics.Common.Utilities;
using Tactics.Common.AI.BehaviourTrees;
using Tactics.Common.Cells;
using Tactics.Common.Highlighters;
using Tactics.Common.Units.Abilities;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Tactics.Common.Units
{
    /// <summary>
    /// A concrete Unity-specific base class representing a unit in the game. 
    /// It handles unit state, movement, combat, interactions with the grid and other units, 
    /// and manages visual indicators for unit selection, movement, and combat actions.
    /// </summary>
    [ExecuteInEditMode]
    public class Unit : MonoBehaviour, IUnit, INamedUnit, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private MoveComponent _moveComponent;
        private CombatComponent _combatComponent;
        private BuffComponent _buffComponent;

        public event Action<IUnit> UnitSelected;
        public event Action<IUnit> UnitDeselected;

        public event Action<IUnit> UnitClicked;
        public event Action<IUnit> UnitHighlighted;
        public event Action<IUnit> UnitDehighlighted;

        public event Action<UnitAttackedEventArgs> UnitAttacked;
        public event Action<UnitDestroyedEventArgs> UnitDestroyed;
        public event Action<HealthChangedEventArgs> HealthChanged;

        public event Action<UnitMovedEventArgs> UnitMoved;
        public event Action<UnitChangedGridPositionEventArgs> UnitLeftCell;
        public event Action<UnitChangedGridPositionEventArgs> UnitEnteredCell;
        public event Action<UnitPositionChangedEventArgs> UnitWorldPositionChanged;

        public event Action<AbilityUsedEventArgs> AbilityUsed;

        [SerializeField] private List<Highlighter> _unMarkFn;
        [SerializeField] private List<Highlighter> _markAsSelectedFn;
        [SerializeField] private List<Highlighter> _markAsFriendlyFn;
        [SerializeField] private List<Highlighter> _markAsFinishedFn;
        [SerializeField] private List<Highlighter> _markAsTargetable;
        [SerializeField] private List<Highlighter> _markAsAttackingFn;
        [SerializeField] private List<Highlighter> _markAsDefendingFn;
        [SerializeField] private List<Highlighter> _markAsMoving;
        [SerializeField] private List<Highlighter> _unMarkAsMoving;
        [SerializeField] private List<Highlighter> _markAsDestroyedFn;

        [SerializeField] private List<AbilityConfig> _abilityConfigs;
        private List<IAbility> _baseAbilities;

        [SerializeField] Cell _currentCell;
        public virtual ICell CurrentCell { get { return _currentCell; } set { _currentCell = value as Cell; } }
        public Vector3Impl WorldPosition { get { return new Vector3Impl(transform.position.x, transform.position.y, transform.position.z); } set { transform.position = new Vector3(value.x, value.y, value.z); } }

        [SerializeField] private int _playerNumber;
        public int PlayerNumber { get { return _playerNumber; } set { _playerNumber = value; } }

        public int UnitID { get; set; }

        public string UnitName => gameObject.name;

        public ITreeNode BehaviourTree { get { return _behaviourTreeResource.BehaviourTree; } }
        [SerializeField] protected BehaviourTreeResource _behaviourTreeResource;

        [SerializeField] float _actionPoints = 1;
        public float ActionPoints { get { return _actionPoints; } set { _actionPoints = value; } }
        public float MaxActionPoints { get; set; }

        [SerializeField] private float _health = 10;
        public float Health { get { return _health; } set { _health = value; } }
        public float MaxHealth { get; set; }
        [SerializeField] private float _mana = 0;
        public float Mana { get { return _mana; } set { _mana = Mathf.Clamp(value, 0, MaxMana); } }
        public float MaxMana { get; set; }

        [SerializeField] private float _movementPoints = 5;
        public float MovementPoints { get { return _movementPoints; } set { _movementPoints = value; } }
        public float MaxMovementPoints { get; set; }
        [SerializeField] private float _movementAnimationSpeed = 1;
        public float MovementAnimationSpeed { get { return _movementAnimationSpeed; } set { _movementAnimationSpeed = value; } }
        [SerializeField] private float _speed = 5f;
        public virtual float Speed { get { return _speed; } set { _speed = value; } }

        [SerializeField] private int _strength = 5;
        public int Strength { get { return _strength; } set { _strength = value; } }
        [SerializeField] private int _agility = 5;
        public int Agility { get { return _agility; } set { _agility = value; } }
        [SerializeField] private int _constitution = 5;
        public int Constitution { get { return _constitution; } set { _constitution = value; } }
        [SerializeField] private int _intelligence = 5;
        public int Intelligence { get { return _intelligence; } set { _intelligence = value; } }
        [SerializeField] private int _charisma = 5;
        public int Charisma { get { return _charisma; } set { _charisma = value; } }
        [SerializeField] private int _luck = 5;
        public int Luck { get { return _luck; } set { _luck = value; } }

        [SerializeField] private int _attackRange = 1;
        public int AttackRange { get { return _attackRange; } set { _attackRange = value; } }
        [SerializeField] private int _attackFactor = 1;
        public int AttackFactor { get { return _attackFactor; } set { _attackFactor = value; } }
        [SerializeField] private int _defenceFactor = 1;
        public int DefenceFactor { get { return _defenceFactor; } set { _defenceFactor = value; } }

        /// <summary>
        /// The buff component that manages buffs for this unit.
        /// </summary>
        public BuffComponent BuffComponent => _buffComponent;

        /// <summary>
        /// Cancellation token source used to cancel ongoing visual defense highlight effects when the unit is destroyed.
        /// </summary>
        CancellationTokenSource _highlightCancellationTokenSource = new CancellationTokenSource();

        public virtual void OnPointerClick(PointerEventData eventData)
        {
            UnitClicked?.Invoke(this);
        }

        public virtual void OnPointerEnter(PointerEventData eventData)
        {
            UnitHighlighted?.Invoke(this);
        }

        public virtual void OnPointerExit(PointerEventData eventData)
        {
            UnitDehighlighted?.Invoke(this);
        }

        public virtual void Initialize(IGridController gridController)
        {
            _moveComponent = new UnityMoveComponent(this);
            _combatComponent = new CombatComponent(this);
            _buffComponent = new BuffComponent(this);
            _behaviourTreeResource?.Initialize(this, gridController);

            MaxActionPoints = ActionPoints;
            RecalculateDerivedStats();
            Health = MaxHealth;
            Mana = MaxMana;
            MovementPoints = MaxMovementPoints;

            _baseAbilities = new List<IAbility>();
            if (_abilityConfigs != null)
            {
                foreach (var config in _abilityConfigs)
                {
                    if (config != null)
                    {
                        var ability = config.CreateAbility(this);
                        RegisterAbility(ability, gridController);
                    }
                }
            }
        }

        public virtual void RegisterAbility(IAbility ability, IGridController gridController)
        {
            ability.UnitReference = this;
            ability.Initialize(gridController);
        }

        public virtual IEnumerable<IAbility> GetBaseAbilities()
        {
            return _baseAbilities;
        }

        public virtual void AddBuff(Buff buff)
        {
            _buffComponent.AddBuff(buff);
        }

        public virtual void RemoveBuff(Buff buff)
        {
            _buffComponent.RemoveBuff(buff);
        }

        public virtual IReadOnlyList<Buff> GetActiveBuffs()
        {
            return _buffComponent.GetActiveBuffs();
        }
        
        public virtual void OnTurnStart(IGridController gridController)
        {
            _buffComponent.OnTurnStart(gridController);
        }

        public virtual void OnTurnEnd(IGridController gridController)
        {
            MovementPoints = MaxMovementPoints;
            ActionPoints = MaxActionPoints;
            Mana = Mathf.Min(MaxMana, Mana + Mathf.Max(0, Intelligence));
            _buffComponent.OnTurnEnd(gridController);
        }

        /// <summary>
        /// Removes any visual highlights or marks on the unit.
        /// </summary>
        /// <remarks>
        /// This method uses the backing field <see cref="_unMarkFn"/> to apply the unmarking effects. 
        /// These effects are reusable and can be customized as needed. Since this method is virtual, 
        /// it can be overridden by inheriting classes if a different marking system is preferred.
        /// </remarks>
        /// <returns>A task representing the asynchronous operation.</returns>
        public virtual async Task UnMark()
        {
            foreach (var fn in _unMarkFn)
            {
                await fn.Apply(NoParam.Instance);
            }
        }
        /// <summary>
        /// Applies a visual highlight to indicate that the unit is selected.
        /// </summary>
        /// <remarks>
        /// This method uses the backing field <see cref="_markAsSelectedFn"/> to apply the selection effects. 
        /// These effects are reusable and can be customized as needed. Since this method is virtual, 
        /// it can be overridden by inheriting classes if a different marking system is preferred.
        /// </remarks>
        /// <returns>A task representing the asynchronous operation.</returns>
        public virtual async Task MarkAsSelected()
        {
            foreach (var fn in _markAsSelectedFn)
            {
                await fn.Apply(NoParam.Instance);
            }
        }
        /// <summary>
        /// Applies a visual highlight to indicate that the unit is friendly.
        /// </summary>
        /// <remarks>
        /// This method uses the backing field <see cref="_markAsFriendlyFn"/> to apply the friendly unit effects. 
        /// These effects are reusable and can be customized as needed. Since this method is virtual, 
        /// it can be overridden by inheriting classes if a different marking system is preferred.
        /// </remarks>
        /// <returns>A task representing the asynchronous operation.</returns>
        public virtual async Task MarkAsFriendly()
        {
            foreach (var fn in _markAsFriendlyFn)
            {
                await fn.Apply(NoParam.Instance);
            }
        }
        /// <summary>
        /// Applies a visual highlight to indicate that the unit has completed its actions for the turn.
        /// </summary>
        /// <remarks>
        /// This method uses the backing field <see cref="_markAsFinishedFn"/> to apply the finished unit effects. 
        /// These effects are reusable and can be customized as needed. Since this method is virtual, 
        /// it can be overridden by inheriting classes if a different marking system is preferred.
        /// </remarks>
        /// <returns>A task representing the asynchronous operation.</returns>
        public virtual async Task MarkAsFinished()
        {
            foreach (var fn in _markAsFinishedFn)
            {
                await fn.Apply(NoParam.Instance);
            }
        }
        /// <summary>
        /// Applies a visual highlight to indicate that the unit can be targeted for actions such as attacks.
        /// </summary>
        /// <remarks>
        /// This method uses the backing field <see cref="_markAsTargetable"/> to apply the reachable enemy effects. 
        /// These effects are reusable and can be customized as needed. Since this method is virtual, 
        /// it can be overridden by inheriting classes if a different marking system is preferred.
        /// </remarks>
        /// <returns>A task representing the asynchronous operation.</returns>
        public virtual async Task MarkAsTargetable()
        {
            foreach (var fn in _markAsTargetable)
            {
                await fn.Apply(NoParam.Instance);
            }
        }
        /// <summary>
        /// Applies a visual highlight to indicate that the unit is attacking another unit.
        /// </summary>
        /// <remarks>
        /// This method uses the backing field <see cref="_markAsAttackingFn"/> to apply the attacking effects. 
        /// These effects are reusable and can be customized as needed. Since this method is virtual, 
        /// it can be overridden by inheriting classes if a different marking system is preferred.
        /// </remarks>
        /// <param name="otherUnit">The unit being attacked.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public virtual async Task MarkAsAttacking(Unit otherUnit)
        {
            foreach (var fn in _markAsAttackingFn)
            {
                await fn.Apply(new CombatHighlightParams(this, otherUnit));
            }
        }
        /// <summary>
        /// Applies a visual highlight to indicate that the unit is defending against an attack.
        /// </summary>
        /// <remarks>
        /// This method uses the backing field <see cref="_markAsDefendingFn"/> to apply the defending effects. 
        /// These effects are reusable and can be customized as needed. Since this method is virtual, 
        /// it can be overridden by inheriting classes if a different marking system is preferred.
        /// </remarks>
        /// <param name="otherUnit">The unit attacking this unit.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public virtual async Task MarkAsDefending(Unit otherUnit)
        {
            foreach (var fn in _markAsDefendingFn)
            {
                if (_highlightCancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }
                await fn.Apply(new CombatHighlightParams(this, otherUnit));
            }
        }

        /// <summary>
        /// Applies a visual effect to indicate that the unit is moving.
        /// </summary>
        /// <remarks>
        /// This method uses the backing field <see cref="_markAsMoving"/> to apply the moving effects. 
        /// These effects are reusable and can be customized as needed. Since this method is virtual, 
        /// it can be overridden by inheriting classes if a different marking system is preferred.
        /// </remarks>
        /// <param name="source">The starting cell of the movement.</param>
        /// <param name="destination">The destination cell of the movement.</param>
        /// <param name="path">The sequence of cells representing the movement path.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public virtual async Task MarkAsMoving(ICell source, ICell destination, IEnumerable<ICell> path)
        {
            foreach (var fn in _markAsMoving)
            {
                await fn.Apply(new MoveHighlightParams(source, destination, path));
            }
        }

        /// <summary>
        /// Removes the visual indication of movement from the unit, typically reversing the effects of <see cref="MarkAsMoving"/>.
        /// </summary>
        /// <remarks>
        /// This method uses the backing field <see cref="_unMarkAsMoving"/> to apply the unmarking effects. 
        /// These effects are reusable and can be customized as needed. Since this method is virtual, 
        /// it can be overridden by inheriting classes if a different marking system is preferred.
        /// </remarks>
        /// <param name="source">The starting cell of the previously marked movement.</param>
        /// <param name="destination">The destination cell of the previously marked movement.</param>
        /// <param name="path">The sequence of cells that represented the movement path.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public virtual async Task UnMarkAsMoving(ICell source, ICell destination, IEnumerable<ICell> path)
        {
            foreach (var fn in _unMarkAsMoving)
            {
                await fn.Apply(new MoveHighlightParams(source, destination, path));
            }
        }

        /// <summary>
        /// Applies a visual effect to indicate that the unit is dead.
        /// </summary>
        /// <remarks>
        /// This method uses the backing field <see cref="_markAsDestroyedFn"/> to apply the defending effects. 
        /// These effects are reusable and can be customized as needed. Since this method is virtual, 
        /// it can be overridden by inheriting classes if a different marking system is preferred.
        /// </remarks>
        /// <returns>A task representing the asynchronous operation.</returns>
        public virtual async Task MarkAsDestroyed()
        {
            foreach (var fn in _markAsDestroyedFn)
            {
                await fn.Apply(NoParam.Instance);
            }
        }

        public virtual bool IsCellMovableTo(ICell cell)
        {
            return _moveComponent.IsCellMovableTo(cell);
        }

        public virtual bool IsCellTraversable(ICell source, ICell destination)
        {
            return _moveComponent.IsCellTraversable(source, destination);
        }

        public virtual float GetMovementCost(ICell source, ICell destination)
        {
            return _moveComponent.GetMovementCost(source, destination);
        }

        public virtual List<ICell> GetAvailableDestinations(IEnumerable<ICell> cells)
        {
            return _moveComponent.GetAvailableDestinations(cells);
        }
        public virtual List<ICell> FindPath(ICell destination, ICellManager cellManager)
        {
            return _moveComponent.FindPath(destination, cellManager);
        }
        public virtual Dictionary<ICell, Dictionary<ICell, float>> GetGraphEdges(ICellManager cellManager)
        {
            return _moveComponent.GetGraphEdges(cellManager);
        }

        public virtual void CachePaths(ICellManager cellManager)
        {
            _moveComponent.CachePaths(cellManager);
        }
        public virtual void InvalidateCache()
        {
            _moveComponent.InvalidateCache();
        }
        public virtual Task MovementAnimation(IEnumerable<ICell> path, ICell destination)
        {
            return _moveComponent.MovementAnimation(path, destination);
        }

        public virtual void ModifyHealth(float healthChangeAmount, IUnit sourceUnit)
        {
            _combatComponent.ModifyHealth(healthChangeAmount, sourceUnit);
        }

        public virtual bool IsUnitAttackable(IUnit otherUnit, ICell otherUnitCell, ICell attackSourceCell)
        {
            return _combatComponent.IsUnitAttackable(otherUnit, otherUnitCell, attackSourceCell);
        }

        public virtual float CalculateDamageDealt(IUnit defender, ICell defenderCell, ICell aggressorCell)
        {
            return _combatComponent.CalculateDamageDealt(defender, defenderCell, aggressorCell);
        }
        public virtual float CalculateDamageDealt(IUnit defender, ICell defenderCell, ICell aggressorCell, bool isRangedDamage)
        {
            return _combatComponent.CalculateDamageDealt(defender, defenderCell, aggressorCell, isRangedDamage);
        }
        public float CalculateDamageDealt(IUnit defender)
        {
            return CalculateDamageDealt(defender, defender.CurrentCell, CurrentCell);
        }
        public virtual float CalculateDamageTaken(IUnit aggressor, float damageDealt, ICell aggressorCell, ICell defenderCell)
        {
            return _combatComponent.CalculateDamageTaken(aggressor, damageDealt, aggressorCell, defenderCell);
        }
        public float CalculateDamageTaken(IUnit aggressor, float damageDealt)
        {
            return CalculateDamageTaken(aggressor, damageDealt, aggressor.CurrentCell, CurrentCell);
        }
        public float CalculateTotalDamage(IUnit defender, ICell defenderCell, ICell agressorCell)
        {
            return _combatComponent.CalculateTotalDamage(defender, defenderCell, agressorCell);
        }
        public float CalculateTotalDamage(IUnit defender, ICell defenderCell, ICell agressorCell, bool isRangedDamage)
        {
            return _combatComponent.CalculateTotalDamage(defender, defenderCell, agressorCell, isRangedDamage);
        }
        public float CalculateTotalDamage(IUnit defender)
        {
            return CalculateTotalDamage(defender, defender.CurrentCell, CurrentCell);
        }

        protected virtual void RecalculateDerivedStats()
        {
            MaxHealth = Mathf.Max(1, Constitution * 4);
            MaxMana = Mathf.Max(0, Charisma * 3);
            MaxMovementPoints = Mathf.Max(1f, Speed);
        }
        public void InvokeUnitSelected()
        {
            UnitSelected?.Invoke(this);
        }

        public void InvokeUnitDeselected()
        {
            UnitDeselected?.Invoke(this);
        }

        public void InvokeUnitClicked()
        {
            UnitClicked?.Invoke(this);
        }

        public void InvokeUnitHighlighted()
        {
            UnitHighlighted?.Invoke(this);
        }

        public void InvokeUnitDehighlighted()
        {
            UnitDehighlighted?.Invoke(this);
        }

        public void InvokeAbilityUsed(AbilityUsedEventArgs args)
        {
            AbilityUsed?.Invoke(args);
        }

        public void InvokeAttacked(UnitAttackedEventArgs eventArgs)
        {
            UnitAttacked?.Invoke(eventArgs);
        }
        public void InvokeDestroyed(UnitDestroyedEventArgs eventArgs)
        {
            UnitDestroyed?.Invoke(eventArgs);
        }

        public void InvokeHealthChanged(HealthChangedEventArgs eventArgs)
        {
            HealthChanged?.Invoke(eventArgs);
        }
        public void InvokeUnitMoved(UnitMovedEventArgs eventArgs)
        {
            UnitMoved?.Invoke(eventArgs);
        }

        public void InvokeUnitLeftCell(UnitChangedGridPositionEventArgs eventArgs)
        {
            UnitLeftCell?.Invoke(eventArgs);
        }
        public void InvokeUnitEnteredCell(UnitChangedGridPositionEventArgs eventArgs)
        {
            UnitEnteredCell?.Invoke(eventArgs);
        }
        public void InvokeUnitPositionChanged(UnitPositionChangedEventArgs eventArgs)
        {
            UnitWorldPositionChanged?.Invoke(eventArgs);
        }

        public virtual void Cleanup(IGridController gridController)
        {
            if (CurrentCell != null)
            {
                CurrentCell.CurrentUnits.Remove(this);
                CurrentCell.IsTaken = CurrentCell.CurrentUnits.Count > 0;

                #if UNITY_EDITOR
                if (!Application.isPlaying && CurrentCell is Cell cell)
                {
                    UnityEditor.Undo.RegisterCompleteObjectUndo(cell, "Clear Unit from Cell");
                    UnityEditor.EditorUtility.SetDirty(cell);
                }
                #endif
            }
        }

        public virtual void OnDestroyed(IGridController gridController)
        {
            _highlightCancellationTokenSource.Cancel();
            _buffComponent.OnUnitDestroyed();
            Destroy(gameObject);
        }

        public void RemoveFromGame()
        {
            InvokeDestroyed(new UnitDestroyedEventArgs(this, null));
        }

        public Task ExecuteAbility(ICommand command, Func<IGridController, Task> preAction, Func<IGridController, Task> postAction, bool isNetworkInvoked = false)
        {
            return UnitHelper.ExecuteAbility(this, command, preAction, postAction, isNetworkInvoked);
        }
        public Task HumanExecuteAbility(ICommand command, IGridController gridController, bool isNetworkInvoked = false)
        {
            return UnitHelper.HumanExecuteAbility(this, command, gridController, isNetworkInvoked);
        }
        public Task HumanExecuteAbility(ICommand command, IGridController gridController, Func<IGridController, Task> preAction, Func<IGridController, Task> postAction, bool isNetworkInvoked = false)
        {
            return UnitHelper.HumanExecuteAbility(this, command, gridController, preAction, postAction, isNetworkInvoked);
        }
        public Task AIExecuteAbility(ICommand command, IGridController gridController, TaskCompletionSource<bool> tcs, bool isNetworkInvoked = false)
        {
            return UnitHelper.AIExecuteAbility(this, command, gridController, tcs, isNetworkInvoked);
        }
        public Task AIExecuteAbility(ICommand command, IGridController gridController, TaskCompletionSource<bool> tcs, Func<IGridController, Task> preAction, Func<IGridController, Task> postAction, bool isNetworkInvoked = false)
        {
            return UnitHelper.AIExecuteAbility(this, command, gridController, tcs, preAction, postAction, isNetworkInvoked);
        }
        private void Reset()
        {
            if(_behaviourTreeResource == null)
            {
                GameObject brain = new GameObject("Brain");
                brain.transform.parent = transform;
                var behaviourTreeResource = brain.AddComponent<RegularBehaviourTreeResource>();
                _behaviourTreeResource = behaviourTreeResource;
            }
        }
    }

    /// <summary>
    /// Parameters used to highlight combat interactions between two units.
    /// </summary>
    public readonly struct CombatHighlightParams : IHighlightParams
    {
        /// <summary>
        /// The unit initiating the highlight effect, whether as the attacker or the defender.
        /// </summary>
        public readonly Unit PrimaryUnit;

        /// <summary>
        /// The unit interacting with the PrimaryUnit in the highlight effect, either the target of the attack or the attacking unit.
        /// </summary>
        public readonly Unit SecondaryUnit;

        public CombatHighlightParams(Unit primaryUnit, Unit secondaryUnit)
        {
            PrimaryUnit = primaryUnit;
            SecondaryUnit = secondaryUnit;
        }
    }

    public readonly struct MoveHighlightParams : IHighlightParams
    {
        public readonly ICell Source;
        public readonly ICell Destination;
        public readonly IEnumerable<ICell> Path;

        public MoveHighlightParams(ICell source, ICell destination, IEnumerable<ICell> path)
        {
            Source = source;
            Destination = destination;
            Path = path;
        }
    }
}
