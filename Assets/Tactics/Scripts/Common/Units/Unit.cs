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
using Tactics.Common.Units.Classes;
using Tactics.Common.Units.Buffs;
using Tactics.Common.Utilities;
using Tactics.Common.Units.Highlight;
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
        public event Action<ManaChangedEventArgs> ManaChanged;

        public event Action<UnitMovedEventArgs> UnitMoved;
        public event Action<UnitChangedGridPositionEventArgs> UnitLeftCell;
        public event Action<UnitChangedGridPositionEventArgs> UnitEnteredCell;
        public event Action<UnitPositionChangedEventArgs> UnitWorldPositionChanged;

        public event Action<AbilityUsedEventArgs> AbilityUsed;

        [SerializeField] private UnitHighlightConfigs _highlightConfigs = new();
        private UnitHighlightManager _highlightManager;

        /// <summary>
        /// The highlight manager for this unit.
        /// </summary>
        public UnitHighlightManager HighlightManager => _highlightManager;

        [SerializeField] private List<AbilityConfig> _abilityConfigs;
        [SerializeField] private RoleConfig _roleConfig;
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

        [SerializeField] private HashSet<string> _usedBasicAbilitiesThisTurn = new();

        [SerializeField] private float _health = 10;
        public float Health { get { return _health; } set { _health = value; } }
        public float MaxHealth { get; set; }
        [SerializeField] private float _mana = 0;
        public float Mana
        {
            get { return _mana; }
            set
            {
                float oldMana = _mana;
                _mana = Mathf.Clamp(value, 0, MaxMana);
                if (!Mathf.Approximately(oldMana, _mana))
                {
                    ManaChanged?.Invoke(new ManaChangedEventArgs(this, oldMana, _mana));
                }
            }
        }
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
        [SerializeField] private float _dodgeRate = 0f;
        public float DodgeRate { get { return _dodgeRate; } set { _dodgeRate = value; } }
        public Sprite Portrait => _roleConfig?.Icon;

        [SerializeField] private int _attackRange = 1;
        public int AttackRange { get { return _attackRange; } set { _attackRange = value; } }
        [SerializeField] private int _attackFactor = 1;
        public int AttackFactor { get { return _attackFactor; } set { _attackFactor = value; } }
        [SerializeField] private int _defenceFactor = 1;
        public int DefenceFactor { get { return _defenceFactor; } set { _defenceFactor = value; } }

        [SerializeField] private float _initiative;
        /// <summary>
        /// Determines turn order. Higher initiative acts first.
        /// </summary>
        public float Initiative { get { return _initiative; } set { _initiative = value; } }

        [SerializeField] private int _reach = 1;
        /// <summary>
        /// Melee attack range in grid cells.
        /// </summary>
        public int Reach { get { return _reach; } set { _reach = value; } }

        [SerializeField] private int _range;
        /// <summary>
        /// Ranged attack range in grid cells.
        /// </summary>
        public int Range { get { return _range; } set { _range = value; } }

        [SerializeField] private bool _isDowned;
        /// <summary>
        /// Whether the unit is incapacitated (HP reduced to zero or below).
        /// </summary>
        public bool IsDowned { get { return _isDowned; } set { _isDowned = value; } }

        /// <summary>
        /// The buff component that manages buffs for this unit.
        /// </summary>
        public BuffComponent BuffComponent => _buffComponent;

        public bool CanAct => _buffComponent?.CanAct ?? true;

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

            // Initialize highlight manager with configs
            _highlightManager = new UnitHighlightManager(this, _highlightConfigs);

            _usedBasicAbilitiesThisTurn = new HashSet<string>();
            RecalculateDerivedStats();
            Health = MaxHealth;
            Mana = Charisma;
            MovementPoints = MaxMovementPoints;

            _baseAbilities = new List<IAbility>();
            
            var abilitySources = _roleConfig != null && _roleConfig.Abilities != null && _roleConfig.Abilities.Count > 0
                ? _roleConfig.Abilities
                : _abilityConfigs;
            
            if (abilitySources != null)
            {
                foreach (var config in abilitySources)
                {
                    if (config != null)
                    {
                        var ability = config.CreateAbility(this);
                        RegisterAbility(ability, gridController);
                    }
                }
            }

            // Ensure default Move ability exists
            if (!_baseAbilities.Any(a => a.DisplayName == "Move"))
            {
                var moveAbility = AbilityConfig.CreateDefaultMoveConfig().CreateAbility(this);
                RegisterAbility(moveAbility, gridController);
            }
        }

        public virtual void RegisterAbility(IAbility ability, IGridController gridController)
        {
            _baseAbilities.Add(ability);
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
            _usedBasicAbilitiesThisTurn.Clear();
            Mana = Mathf.Min(MaxMana, Mana + Mathf.Max(0, Mathf.FloorToInt(Intelligence / 2f)));
            _buffComponent.OnTurnEnd(gridController);
        }

        public virtual bool HasUsedBasicAbilityThisTurn(string abilityName)
        {
            return _usedBasicAbilitiesThisTurn.Contains(abilityName);
        }

        public virtual void MarkBasicAbilityUsed(string abilityName)
        {
            _usedBasicAbilitiesThisTurn.Add(abilityName);
        }

        /// <summary>
        /// Removes any visual highlights or marks on the unit.
        /// </summary>
        public virtual Task UnMark()
        {
            return _highlightManager?.UnMark() ?? Task.CompletedTask;
        }

        /// <summary>
        /// Applies a visual highlight to indicate that the unit is selected.
        /// </summary>
        public virtual Task MarkAsSelected()
        {
            return _highlightManager?.MarkAsSelected() ?? Task.CompletedTask;
        }

        /// <summary>
        /// Applies a visual highlight to indicate that the unit is friendly.
        /// </summary>
        public virtual Task MarkAsFriendly()
        {
            return _highlightManager?.MarkAsFriendly() ?? Task.CompletedTask;
        }

        /// <summary>
        /// Applies a visual highlight to indicate that the unit has completed its actions for the turn.
        /// </summary>
        public virtual Task MarkAsFinished()
        {
            return _highlightManager?.MarkAsFinished() ?? Task.CompletedTask;
        }

        /// <summary>
        /// Applies a visual highlight to indicate that the unit can be targeted for actions such as attacks.
        /// </summary>
        public virtual Task MarkAsTargetable()
        {
            return _highlightManager?.MarkAsTargetable() ?? Task.CompletedTask;
        }

        /// <summary>
        /// Applies a visual highlight to indicate that the unit is attacking another unit.
        /// </summary>
        public virtual Task MarkAsAttacking(Unit otherUnit)
        {
            return _highlightManager?.MarkAsAttacking(otherUnit) ?? Task.CompletedTask;
        }

        /// <summary>
        /// Applies a visual highlight to indicate that the unit is defending against an attack.
        /// </summary>
        public virtual Task MarkAsDefending(Unit otherUnit)
        {
            return _highlightManager?.MarkAsDefending(otherUnit) ?? Task.CompletedTask;
        }

        /// <summary>
        /// Applies a visual effect to indicate that the unit is moving.
        /// </summary>
        public virtual Task MarkAsMoving(ICell source, ICell destination, IEnumerable<ICell> path)
        {
            return _highlightManager?.MarkAsMoving(source, destination, path) ?? Task.CompletedTask;
        }

        /// <summary>
        /// Removes the visual indication of movement from the unit.
        /// </summary>
        public virtual Task UnMarkAsMoving(ICell source, ICell destination, IEnumerable<ICell> path)
        {
            return _highlightManager?.UnMarkAsMoving(source, destination, path) ?? Task.CompletedTask;
        }

        /// <summary>
        /// Applies a visual effect to indicate that the unit is destroyed.
        /// </summary>
        public virtual Task MarkAsDestroyed()
        {
            return _highlightManager?.MarkAsDestroyed() ?? Task.CompletedTask;
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

        public float CalculateExpectedTotalDamage(IUnit defender)
        {
            return _combatComponent.CalculateExpectedTotalDamage(defender);
        }

        protected virtual void RecalculateDerivedStats()
        {
            MaxHealth = Mathf.Max(1, Constitution * 4);
            MaxMana = Mathf.Max(0, Charisma * 3);
            MaxMovementPoints = Mathf.Max(1f, Speed);
            Initiative = Speed * 2;
        }
        /// <summary>
        /// Restores HP and MP after battle. If the unit was downed, it recovers.
        /// </summary>
        public virtual void PostBattleRecovery()
        {
            Health = Mathf.Min(MaxHealth, Health + Constitution * 2);
            Mana = Mathf.Min(MaxMana, Mana + Charisma);
            IsDowned = false;
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
        public void InvokeManaChanged(ManaChangedEventArgs eventArgs)
        {
            ManaChanged?.Invoke(eventArgs);
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
            _highlightManager?.CancelAllHighlights();
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
    public readonly struct CombatHighlightParams : Highlight.IHighlightParams
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

    public readonly struct MoveHighlightParams : Highlight.IHighlightParams
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
