using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tactics.Common.AI.MonsterAI;
using Tactics.Common.Battle;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Players;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using Tactics.Consumables;
using Tactics.Roster;
using Tactics.Common.Units.Classes;
using Tactics.Common.Units.Buffs;
using Tactics.Common.Utilities;
using Tactics.AssetPipeline;
using Tactics.Common.Units.Highlight;
using Tactics.Runtime.Utilities;
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
        private IGridController _gridController;

        public event Action<IUnit> UnitSelected;
        public event Action<IUnit> UnitDeselected;

        public event Action<IUnit> UnitClicked;
        public event Action<IUnit> UnitHighlighted;
        public event Action<IUnit> UnitDehighlighted;

        public event Action<UnitAttackedEventArgs> UnitAttacked;
        public event Action<UnitDestroyedEventArgs> UnitDestroyed;
        public event Action<HealthChangedEventArgs> HealthChanged;
        public event Action<ManaChangedEventArgs> ManaChanged;
        public event Action<TurnEndManaRestoredEventArgs> TurnEndManaRestored;

        public event Action<UnitMovedEventArgs> UnitMoved;
        public event Action<UnitChangedGridPositionEventArgs> UnitLeftCell;
        public event Action<UnitChangedGridPositionEventArgs> UnitEnteredCell;
        public event Action<UnitPositionChangedEventArgs> UnitWorldPositionChanged;

        public event Action<AbilityUsedEventArgs> AbilityUsed;
        public event Action<string> BasicAbilityUsed;
        public event Action<FacingChangedEventArgs> FacingChanged;

        public event Action<BuffChangedEventArgs> BuffChanged;

        [SerializeField] private UnitHighlightConfigs _highlightConfigs = new();
        private UnitHighlightManager _highlightManager;

        /// <summary>
        /// The highlight manager for this unit.
        /// </summary>
        public UnitHighlightManager HighlightManager => _highlightManager;

        [SerializeField] private List<AbilityConfig> _abilityConfigs;
        [SerializeField] private RoleConfig _roleConfig;
        private List<IAbility> _baseAbilities;
        private bool _useInjectedAbilityConfigs;
        private bool _hasExplicitLearnedSkillLoadout;
        private readonly Dictionary<string, int> _learnedSkillLevels = new(System.StringComparer.Ordinal);

        [SerializeField] Cell _currentCell;
        public virtual ICell CurrentCell { get { return _currentCell; } set { _currentCell = value as Cell; } }
        public Vector3Impl WorldPosition { get { return new Vector3Impl(transform.position.x, transform.position.y, transform.position.z); } set { transform.position = new Vector3(value.x, value.y, value.z); } }

        [SerializeField] private int _playerNumber;
        public int PlayerNumber { get { return _playerNumber; } set { _playerNumber = value; } }

        public int UnitID { get; set; }

        public string UnitName => gameObject.name;

        [SerializeField] private AiBrainAsset _aiBrainAsset;

        /// <summary>
        /// 新 AI 脑资产。如果设置了此字段，则使用新 AI 系统而不是行为树。
        /// </summary>
        public AiBrainAsset AiBrainAsset => _aiBrainAsset;

        public void ApplyAiBrain(AiBrainAsset brainAsset)
        {
            _aiBrainAsset = brainAsset;
        }

        /// <summary>
        /// Replaces prefab-authored ability configs before unit initialization.
        /// </summary>
        public void ApplyAbilityConfigs(IEnumerable<AbilityConfig> abilityConfigs)
        {
            _abilityConfigs = abilityConfigs?.Where(config => config != null).Distinct().ToList()
                ?? new List<AbilityConfig>();
            _useInjectedAbilityConfigs = true;
        }

        /// <summary>
        /// Supplies the persisted Pure Run skill levels used by passive runtime hooks.
        /// </summary>
        public void ApplyLearnedSkillLevels(IEnumerable<CharacterDefinition.LearnedSkill> learnedSkills)
        {
            _learnedSkillLevels.Clear();
            _hasExplicitLearnedSkillLoadout = true;
            if (learnedSkills == null)
                return;

            foreach (var learned in learnedSkills)
            {
                if (learned == null || string.IsNullOrWhiteSpace(learned.SkillId))
                    continue;

                if (!_learnedSkillLevels.TryGetValue(learned.SkillId, out int currentLevel) ||
                    learned.Level > currentLevel)
                {
                    _learnedSkillLevels[learned.SkillId] = Mathf.Max(1, learned.Level);
                }
            }
        }

        public int GetLearnedSkillLevel(string skillId) =>
            !string.IsNullOrWhiteSpace(skillId) && _learnedSkillLevels.TryGetValue(skillId, out int level)
                ? level
                : 0;

        public bool UsesInjectedAbilityConfigs => _useInjectedAbilityConfigs;

        [SerializeField] private HashSet<string> _usedBasicAbilitiesThisTurn = new();
        private Dictionary<string, int> _abilityUsesThisTurn = new(StringComparer.Ordinal);

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
        public virtual float Speed
        {
            get { return Mathf.Max(1f, _speed + (_buffComponent?.SpeedModifier ?? 0f)); }
            set
            {
                _speed = value;
                RefreshDerivedStats();
                BattleInitiativeService.For(_gridController)?.NotifyInitiativeChanged(this);
            }
        }

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

        [SerializeField] private bool _useExplicitInitialFacing;
        [SerializeField] private FacingDirection _initialFacing = FacingDirection.East;
        private FacingDirection _facing = FacingDirection.East;

        public FacingDirection Facing
        {
            get => _facing;
            set
            {
                if (_facing == value)
                {
                    ApplyFacingVisual();
                    return;
                }

                var previous = _facing;
                _facing = value;
                ApplyFacingVisual();
                FacingChanged?.Invoke(new FacingChangedEventArgs(previous, _facing));
            }
        }

        public string FacingVisualKey => _facing.ToString();

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

        [SerializeField] private bool _isCorpse;
        /// <summary>
        /// 尸体标记，敌人死亡后原地留下的尸体。
        /// </summary>
        public bool IsCorpse { get { return _isCorpse; } set { _isCorpse = value; } }

        [SerializeField] private bool _canReceiveHealing = true;
        /// <summary>
        /// Whether positive health changes can restore this unit's health.
        /// Reanimated summons disable this while remaining valid heal targets.
        /// </summary>
        public bool CanReceiveHealing { get { return _canReceiveHealing; } set { _canReceiveHealing = value; } }

        [SerializeField] private int _ownerUnitId = -1;
        /// <summary>
        /// 召唤物归属 ID，-1 表示无归属。
        /// </summary>
        public int OwnerUnitId { get { return _ownerUnitId; } set { _ownerUnitId = value; } }

        private IUnit _summonedUnit;
        /// <summary>
        /// 当前召唤的单位引用（死灵法师持有），null 表示无召唤物。
        /// </summary>
        public IUnit SummonedUnit { get { return _summonedUnit; } set { _summonedUnit = value; } }

        private IUnit _ownerUnit;
        /// <summary>
        /// 召唤者的直接引用（骷髅持有），null 表示无归属。
        /// </summary>
        public IUnit OwnerUnit { get { return _ownerUnit; } set { _ownerUnit = value; } }

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
            _gridController = gridController;
            _moveComponent = new UnityMoveComponent(this);
            _combatComponent = new CombatComponent(this);
            EnsureBuffComponent().BindGridController(gridController);

            Facing = ResolveInitialFacing(gridController);

            // Initialize highlight manager with configs
            _highlightManager = new UnitHighlightManager(this, _highlightConfigs);

            _usedBasicAbilitiesThisTurn = new HashSet<string>();
            _abilityUsesThisTurn = new Dictionary<string, int>(StringComparer.Ordinal);
            RecalculateDerivedStats();
            Health = MaxHealth;
            Mana = Charisma;
            MovementPoints = MaxMovementPoints;
            if (TryGetComponent<Tactics.Common.Battle.EncounterUnitRuntimeModifiers>(out var encounterModifiers))
                encounterModifiers.ApplyAfterUnitInitialization(this);

            bool hasCombatTechniques = _hasExplicitLearnedSkillLoadout
                ? GetLearnedSkillLevel("amazon.combat_techniques") > 0
                : !_useInjectedAbilityConfigs && _roleConfig != null && _roleConfig.RoleType == RoleType.Amazon;
            if (hasCombatTechniques)
                CombatComponent.EnableCombatTechniques(this,
                    _hasExplicitLearnedSkillLoadout ? GetLearnedSkillLevel("amazon.combat_techniques") : 1);
            else
                CombatComponent.DisableCombatTechniques(this);

            _baseAbilities = new List<IAbility>();
            
            var abilitySources = _useInjectedAbilityConfigs
                ? _abilityConfigs
                : _roleConfig != null && _roleConfig.Abilities != null && _roleConfig.Abilities.Count > 0
                    ? _roleConfig.Abilities
                    : _abilityConfigs;
            
            if (abilitySources != null)
            {
                foreach (var config in abilitySources)
                {
                    if (config != null)
                    {
                        var ability = config.CreateAbility(this);
                        if (ability != null)
                        {
                            RegisterAbility(ability, gridController);
                        }
                        else
                        {
                            TLog.Warning($"[Unit] {gameObject.name}: AbilityConfig '{config.DisplayName}' returned null from CreateAbility. Skipping.");
                        }
                    }
                }
            }

            if (!_baseAbilities.Any(a => a.DisplayName == "Move"))
            {
                SkillGraphAbilityConfig moveConfig = null;

                if (GameAssetManager.Instance != null)
                {
                    moveConfig = GameAssetManager.Instance.Load<SkillGraphAbilityConfig>(
                        "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Move_Graph_Ability.asset");
                }

                moveConfig ??= SkillGraphAbilityConfig.CreateDefaultMoveConfig();
                RegisterAbility(moveConfig.CreateAbility(this), gridController);
            }

            var rosterLink = GetComponent<RosterCharacterLink>();
            if (rosterLink != null && !string.IsNullOrWhiteSpace(rosterLink.CharacterId))
            {
                var adventureState = PlayerAdventureStateStore.LoadRepairAndSave();
                var character = adventureState?.Roster?.FirstOrDefault(candidate =>
                    candidate?.Id == rosterLink.CharacterId);
                if (adventureState?.IsPureRun == true &&
                    character != null &&
                    !string.IsNullOrWhiteSpace(character.CarriedConsumableInstanceId))
                {
                    var item = adventureState.ConsumableInstances?.FirstOrDefault(candidate =>
                        candidate?.InstanceId == character.CarriedConsumableInstanceId);
                    var ability = ConsumableAbilityFactory.Create(this, item, character.Id);
                    if (ability != null)
                        RegisterAbility(ability, gridController);
                }
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
            if (AmazonBattleState.IsDecoy(this))
                return;
            EnsureBuffComponent().AddBuff(buff);
        }

        public virtual void RemoveBuff(Buff buff)
        {
            _buffComponent?.RemoveBuff(buff);
        }

        public virtual IReadOnlyList<Buff> GetActiveBuffs()
        {
            return _buffComponent?.GetActiveBuffs() ?? Array.Empty<Buff>();
        }

        private BuffComponent EnsureBuffComponent()
        {
            if (_buffComponent != null)
                return _buffComponent;

            _buffComponent = new BuffComponent(this, _gridController);
            _buffComponent.BuffChanged += args => BuffChanged?.Invoke(args);
            return _buffComponent;
        }
        
        public virtual void OnTurnStart(IGridController gridController)
        {
            PrepareForTurn();
            AmazonBattleState.For(gridController)?.OnOwnerTurnStart(this);
            _buffComponent.OnTurnStart(gridController);
        }

        public virtual void PrepareForTurn()
        {
            MovementPoints = MaxMovementPoints;
            (_usedBasicAbilitiesThisTurn ??= new HashSet<string>()).Clear();
            (_abilityUsesThisTurn ??= new Dictionary<string, int>(StringComparer.Ordinal)).Clear();
        }

        public virtual void OnTurnEnd(IGridController gridController)
        {
            if (this == null)
                return;

            float manaBefore = Mana;
            Mana = Mathf.Min(MaxMana, Mana + Mathf.Max(0, Intelligence));
            if (Mana > manaBefore && this != null)
                TurnEndManaRestored?.Invoke(new TurnEndManaRestoredEventArgs(
                    this,
                    manaBefore,
                    Mana,
                    transform.position));

            _buffComponent.OnTurnEnd(gridController);
            AmazonBattleState.For(gridController)?.OnOwnerTurnEnd(this);
            SummonRegistry.For(gridController)?.NotifyActionCompleted(this);
        }

        public virtual bool HasUsedBasicAbilityThisTurn(string abilityName)
        {
            return !string.IsNullOrEmpty(abilityName) &&
                   (_usedBasicAbilitiesThisTurn ??= new HashSet<string>()).Contains(abilityName);
        }

        public virtual void MarkBasicAbilityUsed(string abilityName)
        {
            if (string.IsNullOrEmpty(abilityName))
                return;

            if ((_usedBasicAbilitiesThisTurn ??= new HashSet<string>()).Add(abilityName))
                MarkAbilityUsedThisTurn(abilityName);

            InvokeBasicAbilityUsed(abilityName);
        }

        public virtual int GetAbilityUseCountThisTurn(string abilityName)
        {
            if (string.IsNullOrEmpty(abilityName))
                return 0;

            return (_abilityUsesThisTurn ??= new Dictionary<string, int>(StringComparer.Ordinal))
                .TryGetValue(abilityName, out int count)
                    ? count
                    : 0;
        }

        public virtual void MarkAbilityUsedThisTurn(string abilityName)
        {
            if (string.IsNullOrEmpty(abilityName))
                return;

            var uses = _abilityUsesThisTurn ??= new Dictionary<string, int>(StringComparer.Ordinal);
            uses[abilityName] = uses.TryGetValue(abilityName, out int count) ? count + 1 : 1;
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

        public virtual void RefreshDerivedStats()
        {
            RecalculateDerivedStats();
            MovementPoints = Mathf.Min(MovementPoints, MaxMovementPoints);
        }

        public void SetInitialFacing(FacingDirection facing)
        {
            _useExplicitInitialFacing = true;
            _initialFacing = facing;
            Facing = facing;
        }

        private FacingDirection ResolveInitialFacing(IGridController gridController)
        {
            if (_useExplicitInitialFacing)
                return _initialFacing;

            var player = gridController?.PlayerManager?.GetPlayerByNumber(PlayerNumber);
            if (player != null)
                return player.PlayerType == PlayerType.HumanPlayer
                    ? FacingDirection.East
                    : FacingDirection.West;

            return PlayerNumber == 0 ? FacingDirection.East : FacingDirection.West;
        }

        private void ApplyFacingVisual()
        {
            foreach (var animator in GetComponentsInChildren<Animator>(true))
            {
                if (animator == null || animator.runtimeAnimatorController == null)
                    continue;

                foreach (var parameter in animator.parameters)
                {
                    if (parameter.type == AnimatorControllerParameterType.Int && parameter.name == "Facing")
                        animator.SetInteger(parameter.nameHash, (int)_facing);
                    else if (parameter.type == AnimatorControllerParameterType.Float && parameter.name == "DirectionX")
                        animator.SetFloat(parameter.nameHash, _facing == FacingDirection.East ? 1f : _facing == FacingDirection.West ? -1f : 0f);
                    else if (parameter.type == AnimatorControllerParameterType.Float && parameter.name == "DirectionY")
                        animator.SetFloat(parameter.nameHash, _facing == FacingDirection.North ? 1f : _facing == FacingDirection.South ? -1f : 0f);
                }
            }

            var fourDirectionVisual = GetComponent<FourDirectionSpriteVisual>();
            if (fourDirectionVisual != null && fourDirectionVisual.TryApply(_facing))
                return;

            if (_facing is not FacingDirection.East and not FacingDirection.West)
                return;

            bool flipX = _facing == FacingDirection.West;
            foreach (var spriteRenderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (spriteRenderer != null && !spriteRenderer.gameObject.name.Contains("Shadow", StringComparison.OrdinalIgnoreCase))
                    spriteRenderer.flipX = flipX;
            }
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

        public void InvokeBasicAbilityUsed(string abilityName)
        {
            BasicAbilityUsed?.Invoke(abilityName);
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
            CombatComponent.DisableCombatTechniques(this);

            // Linked death: if this unit has a summoned unit, kill it
            if (_summonedUnit != null && !_summonedUnit.IsDowned)
            {
                _summonedUnit.OwnerUnitId = -1;
                _summonedUnit.ModifyHealth(-_summonedUnit.Health - 1, null);
                _summonedUnit = null;
            }

            // Linked death: if this unit is summoned, clear owner reference
            if (_ownerUnitId >= 0)
            {
                _ownerUnitId = -1;
            }

            _highlightCancellationTokenSource.Cancel();
            _highlightManager?.CancelAllHighlights();
            _buffComponent.OnUnitDestroyed();
            Destroy(gameObject);
        }

        protected virtual void OnDestroy()
        {
            CombatComponent.DisableCombatTechniques(this);
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
