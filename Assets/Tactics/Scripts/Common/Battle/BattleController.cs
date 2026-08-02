using System;
using Tactics.Runtime.Utilities;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Tactics.AssetPipeline;
using Tactics.Common.AI.MonsterAI;
using Tactics.Common.Battle.Runtime;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Controllers.GameResolvers;
using Tactics.Common.Controllers.GridStates;
using Tactics.Common.Controllers.TurnResolvers;
using Tactics.Common.Network;
using Tactics.Common.Players;
using Tactics.Common.Units;
using Tactics.Common.Units.Classes;
using Tactics.Common.Units.Buffs;
using Tactics.Common.Units.Abilities;
using Tactics.Common.Utilities;
using Tactics.Roguelike;
using Tactics.Roster;
using Tactics.UI;
using Tactics.Common.Interactables;
using Tactics.Units;
using Tactics.Runtime.BattleLog;

namespace Tactics.Common.Battle
{
    [Serializable]
    public class RolePrefabMapping
    {
        public RoleType RoleType;
        public GameObject Prefab;
        public Vector2Int StartingCell;
    }

    /// <summary>
    /// 战斗控制器，统一管理网格、单位、玩家、回合以及战斗生命周期。
    /// 合并了原 UnityGridController 的职责，直接实现 IGridController 接口。
    /// </summary>
    public sealed class BattleController : MonoBehaviourSingleton<BattleController>, IGridController, IPlayerManager, IUnitManager
    {
        #region Battle Events

        public event Action BattleStarted;
        public event Action<GameResult> BattleEnded;
        public bool IsBattleActive { get; private set; }

        #endregion

        #region IGridController Implementation (from UnityGridController)

        /// <summary>
        /// 当游戏开始时触发。
        /// </summary>
        public event Action GameStarted;

        /// <summary>
        /// 当游戏初始化完成时触发。
        /// </summary>
        public event Action GameInitialized;

        /// <summary>
        /// 当游戏结束时触发，提供游戏结果。
        /// </summary>
        public event Action<GameResult> GameEnded;

        /// <summary>
        /// 当新回合开始时触发，提供回合上下文。
        /// </summary>
        public event Action<TurnTransitionParams> TurnStarted;

        /// <summary>
        /// 当当前回合结束时触发，提供回合上下文。
        /// </summary>
        public event Action<TurnTransitionParams> TurnEnded;

        public ICellManager CellManager
        {
            get => _controller.CellManager;
            set { _controller.CellManager = value; _cellManager = value as UnityCellManager; }
        }

        public IUnitManager UnitManager
        {
            get => _controller.UnitManager;
            set => _controller.UnitManager = value;
        }

        public IPlayerManager PlayerManager
        {
            get => this;
            set { /* ignored - BattleController manages players internally */ }
        }

        public ITurnResolver TurnResolver
        {
            get => _controller.TurnResolver ?? (_controller.TurnResolver = new Tactics.Common.Controllers.TurnResolvers.SubsequentTurnResolverImpl());
            set { _controller.TurnResolver = value; _turnResolver = value; }
        }

        public TurnContext TurnContext => _controller.TurnContext;
        public int CurrentRound => _controller.CurrentRound;
        public Transform UnitContainerTransform => _unitContainer;

        /// <summary>
        /// 战斗运行时作用域，管理所有异步操作的生命周期。
        /// </summary>
        public IBattleRuntimeScope RuntimeScope { get; private set; }

        /// <summary>
        /// 最近一次运行时作用域清理期间观察到的非取消异常。
        /// 清理仍会完成，调用方可通过该属性显式检查 drain/dispose 失败。
        /// </summary>
        public Exception RuntimeScopeTeardownException
        {
            get
            {
                lock (_runtimeScopeTeardownGate)
                {
                    return _runtimeScopeTeardownException;
                }
            }
        }

        /// <summary>
        /// 当设置为 true 时，OnAbilityUsed 跳过活跃单位检查，允许 AI 测试直接执行命令。
        /// </summary>
        public bool BypassActiveUnitCheck
        {
            get => _controller.BypassActiveUnitCheck;
            set => _controller.BypassActiveUnitCheck = value;
        }

        /// <summary>
        /// 当设置为 true 时，禁止 AI 自动执行 Play()，用于测试环境。
        /// </summary>
        public bool DisableAiAutoPlay
        {
            get => _controller.DisableAiAutoPlay;
            set => _controller.DisableAiAutoPlay = value;
        }

        public GridState GridState
        {
            get => _controller.GridState;
            set => _controller.GridState = value;
        }

        #endregion

        #region Serialized Fields

        /// <summary>
        /// 是否在场景加载时立即开始游戏。
        /// </summary>
        [SerializeField] private bool _startImmediatelly = true;
        [SerializeField] private UnityCellManager _cellManager;
        [SerializeReference] private ITurnResolver _turnResolver;
        [SerializeField] private Transform _unitContainer;

        /// <summary>
        /// Player configurations serialized on BattleController.
        /// If empty, players will be auto-configured from scene units.
        /// </summary>
        [SerializeField] private PlayerEntry[] _players;

        /// <summary>
        /// Specifies which PlayerNumber belongs to the local human player.
        /// Set to 0 for auto-detection (fewest units = human).
        /// </summary>
        [SerializeField] private int _localPlayerNumber;

        [Header("Party Spawning")]
        [SerializeField] private int _humanPlayerNumber;
        [SerializeField] private List<RolePrefabMapping> _rolePrefabMappings = new();

        [Header("Test Config")]
        [Tooltip("开启后，BattleController 将绕过正式玩家队伍和正式遭遇加载链路，改用下方测试配置资产。")]
        [SerializeField] private bool _useTestSetup;
        [SerializeField] private BattlePartyTestConfig _testPartyConfig;
        [SerializeField] private BattleEncounterTestConfig _testEncounterConfig;

        [Header("Corpse")]
        [Tooltip("尸体 prefab 在 GameAssetManager 中的路径。单位死亡时通过此路径加载尸体 prefab。")]
        [SerializeField] private string _corpsePrefabPath = "Assets/Tactics/Arts/Prefabs/Units/TestCorpse.prefab";

        #endregion

        #region Private Fields

        private readonly GridController _controller = new GridController();
        private IList<IPlayer> _runtimePlayers;
        private IList<IUnit> _units;
        private int _unitCount;
        private readonly HashSet<string> _loadedPaths = new();
        private readonly Dictionary<string, AbilityConfig> _pureRunAbilityConfigCache = new(StringComparer.Ordinal);
        private readonly Dictionary<ICell, bool> _encounterBlockedCells = new();
        private Action<string> _runtimeAssetReleaseOverrideForTests;
        private readonly object _runtimeScopeTeardownGate = new();
        private Task _runtimeScopeTeardownTask = Task.CompletedTask;
        private IBattleRuntimeScope _tearingDownRuntimeScope;
        private Exception _runtimeScopeTeardownException;
        private bool _battleStartInProgress;
        private bool _isDestroying;
        private int _lifecycleGeneration;
        private bool UseTestSetupForCurrentBattle =>
            _useTestSetup && string.IsNullOrEmpty(RoguelikeMapRuntimeState.PendingBattleNodeId);

        #endregion

        #region Unity Lifecycle

        protected override void Awake()
        {
            base.Awake();

            // 初始化 GridController 的依赖
            _controller.CellManager = _cellManager;
            _controller.UnitManager = this;
            _controller.PlayerManager = this;
            _controller.TurnResolver = _turnResolver ?? new Tactics.Common.Controllers.TurnResolvers.SubsequentTurnResolverImpl();
            _controller.CorpsePrefabPath = _corpsePrefabPath;
            _controller.BeforeUnitManagerInitialize = _ =>
            {
                if (UseTestSetupForCurrentBattle && _testPartyConfig != null)
                    SpawnTestPartyUnits();
                else
                    SpawnPartyUnits();
                SpawnEncounterUnits();
            };

            // Initialize players (will be configured in IPlayerManager.Initialize after UnitManager is ready)
            if (_players != null && _players.Length > 0)
            {
                InitializePlayers();
            }

            // 订阅 GridController 的事件
            _controller.GameInitialized += OnGameInitialized;
            _controller.GameEnded += OnGameEnded;
            _controller.GameStarted += OnGameStarted;
            _controller.TurnStarted += OnTurnStarted;
            _controller.TurnEnded += OnTurnEnded;

            // 订阅 UnitRemoved 事件用于判断胜负
            UnitRemoved += OnUnitRemoved;

            RoguelikeBattleReturnHandler.Instance.RegisterController(this);
        }

        protected override void OnDestroy()
        {
            _isDestroying = true;
            Interlocked.Increment(ref _lifecycleGeneration);
            Task runtimeTeardownTask = TeardownRuntimeScopeAsync();

            if (_runtimePlayers != null)
            {
                foreach (var aiPlayer in _runtimePlayers.OfType<AIPlayer>())
                    aiPlayer.CancelOngoingAction();
            }

            _controller.GameInitialized -= OnGameInitialized;
            _controller.GameEnded -= OnGameEnded;
            _controller.GameStarted -= OnGameStarted;
            _controller.TurnStarted -= OnTurnStarted;
            _controller.TurnEnded -= OnTurnEnded;

            UnitRemoved -= OnUnitRemoved;

            var manager = GameAssetManager.Instance;
            Action<string> releasePath = _runtimeAssetReleaseOverrideForTests;
            if (releasePath == null && manager != null)
                releasePath = manager.Release;
            string[] loadedPathSnapshot = _loadedPaths.ToArray();
            _loadedPaths.Clear();
            _ = ReleaseLoadedPathsAfterTeardownAsync(
                runtimeTeardownTask,
                loadedPathSnapshot,
                releasePath);
            _pureRunAbilityConfigCache.Clear();
            RestoreEncounterBlockedCells();

            RoguelikeBattleReturnHandler.Instance.UnregisterController(this);
            base.OnDestroy();
        }

        private static async Task ReleaseLoadedPathsAfterTeardownAsync(
            Task runtimeTeardownTask,
            IReadOnlyList<string> loadedPaths,
            Action<string> releasePath)
        {
            try
            {
                await runtimeTeardownTask;
            }
            catch (Exception ex)
            {
                // The fallback release must continue even if a future teardown implementation
                // starts propagating faults instead of reporting them on the controller.
                TLog.Error($"[BattleController] Runtime teardown faulted before asset release: {ex}");
            }

            if (releasePath == null)
                return;

            foreach (string path in loadedPaths)
            {
                try
                {
                    releasePath(path);
                }
                catch (Exception ex)
                {
                    // One invalid release must not prevent the remaining owned paths from draining.
                    TLog.Error($"[BattleController] Failed to release runtime asset '{path}': {ex}");
                }
            }
        }

        private IEnumerator Start()
        {
            if (this == null || gameObject == null) yield break;

            while (GameAssetManager.Instance == null || !GameAssetManager.Instance.IsInitialized)
                yield return null;

            if (_startImmediatelly)
            {
                InitializeGame();
                SyncStartingHp();
                TBattleLog.BeginBattle();
                StartGame();
                _ = StartBattleAsync();
            }
        }

        private void SpawnEncounterUnits()
        {
            if (UseTestSetupForCurrentBattle && _testEncounterConfig != null)
            {
                SpawnTestEncounterUnits();
                SpawnTestEncounterInteractables();
                return;
            }

            var mgr = GameAssetManager.Instance;
            if (mgr == null)
            {
                TLog.Error("[BattleController] GameAssetManager unavailable while spawning encounter units.");
                return;
            }

            Transform container = UnitContainerTransform;
            if (container == null)
                container = transform;

            var existingUnits = FindObjectsByType<TilemapUnit>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var existing in existingUnits)
            {
                if (existing == null || existing.PlayerNumber == _humanPlayerNumber)
                    continue;

                existing.gameObject.SetActive(false);
                Destroy(existing.gameObject);
            }

            var encounterPath = EncounterRuntimeState.GetPendingEncounterPath();
            var encounter = EncounterRuntimeState.TryResolvePendingEncounter(mgr, out var resolvedEncounter, out var resolvedSource)
                ? resolvedEncounter
                : EncounterConfigLoader.Load(encounterPath, mgr);
            if (resolvedEncounter != null)
                encounterPath = resolvedSource;
            if (encounter == null)
            {
                TLog.Warning($"[BattleController] No valid encounter found at '{encounterPath}'.");
                return;
            }

            if (!ApplyEncounterBlockedCells(encounter.BlockedCells))
                return;

            foreach (var unitEntry in encounter.Units)
            {
                SpawnEncounterUnit(unitEntry, container, mgr, encounterPath);
            }
        }

        private void SpawnPartyUnits()
        {
            if (UseTestSetupForCurrentBattle && _testPartyConfig != null)
            {
                // Deferred to BeforeUnitManagerInitialize (after CellManager init)
                return;
            }

            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            if (state?.ActivePartyCharacterIds == null || state.ActivePartyCharacterIds.Count == 0)
            {
                TLog.Warning("[BattleController] No active party characters found.");
                return;
            }

            Transform container = UnitContainerTransform;
            if (container == null)
                container = transform;

            var unitManager = (this as IUnitManager);
            var existingUnits = FindObjectsByType<TilemapUnit>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var existing in existingUnits)
            {
                if (existing.PlayerNumber == _humanPlayerNumber)
                {
                    unitManager.RemoveUnit(existing);
                    Destroy(existing.gameObject);
                }
            }

            var prefabLookup = new Dictionary<RoleType, GameObject>();
            var mgr = GameAssetManager.Instance;

            foreach (var jsonMapping in PlayerAdventureStateStore.TestPrefabMappings)
            {
                if (string.IsNullOrEmpty(jsonMapping.PrefabPath))
                    continue;

                GameObject prefab = null;
                if (mgr != null)
                {
                    var resolvedPath = CharacterDefinition.ResolvePrefabPath(jsonMapping.PrefabPath);
                    if (!string.IsNullOrEmpty(resolvedPath))
                        prefab = mgr.Load<GameObject>(resolvedPath);
                }

                if (prefab != null)
                {
                    _loadedPaths.Add(CharacterDefinition.ResolvePrefabPath(jsonMapping.PrefabPath));
                    prefabLookup[jsonMapping.RoleType] = prefab;
                }
            }

            foreach (var mapping in _rolePrefabMappings)
            {
                if (mapping.Prefab != null && !prefabLookup.ContainsKey(mapping.RoleType))
                    prefabLookup[mapping.RoleType] = mapping.Prefab;
            }

            GameObject fallbackPrefab = prefabLookup.Values.FirstOrDefault();

            var respawnPoints = new List<Transform>();
            var unitManagerGo = container.gameObject;
            if (unitManagerGo != null)
            {
                foreach (Transform child in unitManagerGo.transform)
                {
                    if (child.CompareTag("Respawn"))
                        respawnPoints.Add(child);
                }
            }

            var reservedPartyCells = new HashSet<ICell>();
            for (int i = 0; i < state.ActivePartyCharacterIds.Count; i++)
            {
                string id = state.ActivePartyCharacterIds[i];
                var def = state.Roster.FirstOrDefault(c => c.Id == id);
                if (def == null)
                {
                    TLog.Warning($"[BattleController] Party id '{id}' not in roster; skipping slot {i}.");
                    continue;
                }

                GameObject prefab = null;

                var characterPath = CharacterDefinition.ResolvePrefabPath(def.PrefabPath);
                if (!string.IsNullOrEmpty(characterPath) && mgr != null)
                {
                    prefab = mgr.Load<GameObject>(characterPath);
                    if (prefab != null)
                        _loadedPaths.Add(characterPath);
                }

                if (prefab == null && !prefabLookup.TryGetValue(def.RoleType, out prefab))
                    prefab = null;

                if (prefab == null)
                {
                    prefab = fallbackPrefab;
                    if (prefab == null)
                    {
                        TLog.Error($"[BattleController] No prefab for {def.Id} (path={def.PrefabPath}, role={def.RoleType}) and no fallback available.");
                        continue;
                    }
                    TLog.Warning($"[BattleController] No prefab for {def.Id}, using fallback.");
                }

                var go = Instantiate(prefab, container);
                go.name = $"PartyUnit_{def.DisplayName}";

                var unit = go.GetComponent<TilemapUnit>();
                if (unit == null)
                {
                    TLog.Error($"[BattleController] Prefab for {def.RoleType} does not have a TilemapUnit component.");
                    Destroy(go);
                    continue;
                }

                unit.PlayerNumber = _humanPlayerNumber;

                var roleMapping = _rolePrefabMappings.FirstOrDefault(mapping =>
                    mapping != null && mapping.RoleType == def.RoleType);
                var configuredCell = roleMapping == null
                    ? null
                    : CellManager?.GetCellAt(roleMapping.StartingCell.ToIVector2Int());
                if (configuredCell == null ||
                    reservedPartyCells.Contains(configuredCell) ||
                    !IsCellVisibleToMainCamera(configuredCell))
                {
                    var availableCells = CellManager?.GetCells()
                        .Where(cell => cell != null &&
                            !cell.IsTaken &&
                            cell.CurrentUnits.Count == 0 &&
                            !reservedPartyCells.Contains(cell) &&
                            CellManager.IsCellWalkable(cell))
                        .ToList();
                    configuredCell = availableCells
                        ?.Where(IsCellVisibleToMainCamera)
                        .OrderBy(GetPartySpawnViewportDistance)
                        .ThenBy(cell => cell.GridCoordinates.y)
                        .FirstOrDefault();
                }
                if (configuredCell != null)
                {
                    go.transform.position = configuredCell.WorldPosition.ToVector3();
                    reservedPartyCells.Add(configuredCell);
                }
                else if (i < respawnPoints.Count)
                {
                    go.transform.position = respawnPoints[i].position;
                }
                else
                {
                    var referenceUnit = GameObject.Find("Infantry Blue");
                    if (referenceUnit != null)
                    {
                        go.transform.position = referenceUnit.transform.position + new Vector3(i * 2.5f, 0, 0);
                    }
                    else
                    {
                        TLog.Warning($"[BattleController] No Respawn point for slot {i} and no Infantry Blue reference. Using prefab default position.");
                    }
                }

                CharacterStatsApplicator.ApplyToUnit(def, unit);

                if (state.IsPureRun)
                {
                    PureRunAbilityBinder.Bind(
                        def,
                        unit,
                        path => LoadPureRunAbilityConfig(path, mgr));
                }

                var link = unit.GetComponent<RosterCharacterLink>();
                if (link == null)
                    link = unit.gameObject.AddComponent<RosterCharacterLink>();
                link.CharacterId = def.Id;

                // Apply PendingBuffs from CharacterDefinition (map-layer buffs → combat start)
                if (def.PendingBuffs.Count > 0)
                {
                    foreach (var buffConfig in def.PendingBuffs)
                    {
                        unit.AddBuff(new Buff(buffConfig, null, buffConfig.DefaultDuration));
                    }
                    def.ClearPendingBuffs();
                }
            }
        }

        private static bool IsCellVisibleToMainCamera(ICell cell)
        {
            var camera = Camera.main;
            if (camera == null || cell == null)
                return false;

            Vector3 viewport = camera.WorldToViewportPoint(cell.WorldPosition.ToVector3());
            return viewport.z > 0f &&
                viewport.x >= 0f && viewport.x <= 1f &&
                viewport.y >= 0f && viewport.y <= 1f;
        }

        private static float GetPartySpawnViewportDistance(ICell cell)
        {
            var camera = Camera.main;
            if (camera == null || cell == null)
                return float.MaxValue;

            Vector3 viewport = camera.WorldToViewportPoint(cell.WorldPosition.ToVector3());
            return (new Vector2(viewport.x, viewport.y) - new Vector2(0.25f, 0.5f)).sqrMagnitude;
        }

        private AbilityConfig LoadPureRunAbilityConfig(string configuredPath, GameAssetManager manager)
        {
            if (manager == null || string.IsNullOrWhiteSpace(configuredPath))
                return null;

            string path = GameAssetManager.NormalizeAssetPath(configuredPath);
            if (_pureRunAbilityConfigCache.TryGetValue(path, out var cached))
                return cached;

            var config = manager.Load<AbilityConfig>(path);
            if (config == null)
                return null;

            _pureRunAbilityConfigCache.Add(path, config);
            _loadedPaths.Add(path);
            return config;
        }

        private void SpawnEncounterUnit(EncounterUnitEntry unitEntry, Transform container, GameAssetManager mgr, string encounterPath)
        {
            if (unitEntry == null)
            {
                TLog.Error($"[BattleController] Encounter '{encounterPath}' contains a null unit entry.");
                return;
            }

            string unitLabel = string.IsNullOrWhiteSpace(unitEntry.UnitName) ? unitEntry.MonsterId : unitEntry.UnitName;
            if (!TryGetEncounterCell(unitEntry.SpawnCellX, unitEntry.SpawnCellY, out var spawnCell))
            {
                TLog.Error($"[BattleController] Encounter '{encounterPath}' cannot spawn '{unitLabel}' at ({unitEntry.SpawnCellX},{unitEntry.SpawnCellY}): cell does not exist.");
                return;
            }

            var prefabPath = GameAssetManager.NormalizeAssetPath(unitEntry.UnitPrefabPath);
            var prefab = mgr.Load<GameObject>(prefabPath);
            if (prefab == null)
            {
                TLog.Error($"[BattleController] Encounter '{encounterPath}' cannot spawn '{unitLabel}' at ({unitEntry.SpawnCellX},{unitEntry.SpawnCellY}): prefab not found '{prefabPath}'.");
                return;
            }

            _loadedPaths.Add(prefabPath);
            if (prefab.GetComponent<TilemapUnit>() == null)
            {
                TLog.Error($"[BattleController] Encounter '{encounterPath}' cannot spawn '{unitLabel}' at ({unitEntry.SpawnCellX},{unitEntry.SpawnCellY}): prefab '{prefabPath}' is missing TilemapUnit.");
                return;
            }

            var abilityConfigs = new List<AbilityConfig>();
            if (unitEntry.AbilityConfigPaths != null && unitEntry.AbilityConfigPaths.Count > 0)
            {
                foreach (string configuredPath in unitEntry.AbilityConfigPaths)
                {
                    string abilityPath = GameAssetManager.NormalizeAssetPath(configuredPath);
                    var abilityConfig = mgr.Load<AbilityConfig>(abilityPath);
                    if (abilityConfig == null)
                    {
                        TLog.Error($"[BattleController] Encounter '{encounterPath}' cannot spawn '{unitLabel}' at ({unitEntry.SpawnCellX},{unitEntry.SpawnCellY}): ability config not found '{abilityPath}'.");
                        return;
                    }

                    _loadedPaths.Add(abilityPath);
                    abilityConfigs.Add(abilityConfig);
                }
            }

            AiBrainAsset brain = null;
            if (unitEntry.PlayerNumber != _humanPlayerNumber && !string.IsNullOrWhiteSpace(unitEntry.AiBrainAssetPath))
            {
                var aiPath = GameAssetManager.NormalizeAssetPath(unitEntry.AiBrainAssetPath);
                brain = mgr.Load<AiBrainAsset>(aiPath);
                if (brain == null || !brain.IsValid())
                {
                    TLog.Error($"[BattleController] Encounter '{encounterPath}' cannot spawn '{unitLabel}' at ({unitEntry.SpawnCellX},{unitEntry.SpawnCellY}): AI brain is missing or invalid '{aiPath}'.");
                    return;
                }

                _loadedPaths.Add(aiPath);
            }

            var go = Instantiate(prefab, container);
            go.name = string.IsNullOrWhiteSpace(unitEntry.UnitName) ? prefab.name : unitEntry.UnitName;
            var unit = go.GetComponent<TilemapUnit>();
            unit.PlayerNumber = unitEntry.PlayerNumber;
            var encounterModifiers = go.GetComponent<EncounterUnitRuntimeModifiers>() ?? go.AddComponent<EncounterUnitRuntimeModifiers>();
            encounterModifiers.Configure(unitEntry.MonsterId, unitEntry.HealthMultiplier, unitEntry.OutputMultiplier, unitEntry.MinimumStartingMana);
            if (abilityConfigs.Count > 0)
                unit.ApplyAbilityConfigs(abilityConfigs);

            go.transform.position = spawnCell.WorldPosition.ToVector3();
            unit.CurrentCell = spawnCell;
            if (!spawnCell.CurrentUnits.Contains(unit))
                spawnCell.CurrentUnits.Add(unit);
            spawnCell.IsTaken = true;

            if (brain != null)
            {
                unit.ApplyAiBrain(brain);
            }
            else if (unitEntry.PlayerNumber != _humanPlayerNumber)
            {
                TLog.Info($"[BattleController] Encounter unit '{go.name}' has no AiBrainAssetPath configured. Unit will have no AI.");
            }
        }

        private void SpawnTestPartyUnits()
        {
            if (_testPartyConfig == null)
            {
                TLog.Error("[BattleController] UseTestSetup=true but TestPartyConfig is null. Falling back to production spawn.");
                SpawnPartyUnits();
                return;
            }

            Transform container = UnitContainerTransform ?? transform;
            var unitManager = this as IUnitManager;
            var existingUnits = FindObjectsByType<TilemapUnit>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var existing in existingUnits)
            {
                if (existing.PlayerNumber == _humanPlayerNumber)
                {
                    unitManager.RemoveUnit(existing);
                    Destroy(existing.gameObject);
                }
            }

            var slots = _testPartyConfig.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null)
                {
                    TLog.Warning($"[BattleController] TestPartySlot[{i}] is null. Skipping.");
                    continue;
                }

                if (slot.UnitPrefab == null)
                {
                    TLog.Error($"[BattleController] TestPartySlot[{i}] has null UnitPrefab. Skipping.");
                    continue;
                }

                if (!TryGetTestSetupCell(slot.SpawnCell, $"TestPartySlot[{i}]", out var cell))
                {
                    continue;
                }

                var go = Instantiate(slot.UnitPrefab, container);
                go.name = string.IsNullOrWhiteSpace(slot.DisplayName) ? $"TestPartyUnit_{i}" : slot.DisplayName;

                var unit = go.GetComponent<TilemapUnit>();
                if (unit == null)
                {
                    TLog.Error($"[BattleController] TestPartySlot[{i}] prefab missing TilemapUnit.");
                    Destroy(go);
                    continue;
                }

                unit.PlayerNumber = _humanPlayerNumber;
                unit.Strength = slot.Strength;
                unit.Agility = slot.Agility;
                unit.Constitution = slot.Constitution;
                unit.Intelligence = slot.Intelligence;
                unit.Charisma = slot.Charisma;
                unit.Luck = slot.Luck;
                unit.Speed = slot.Speed;
                unit.AttackFactor = slot.AttackFactor;
                unit.DefenceFactor = slot.DefenceFactor;

                go.transform.position = cell.WorldPosition.ToVector3();
                unit.CurrentCell = cell;
                if (!cell.CurrentUnits.Contains(unit))
                    cell.CurrentUnits.Add(unit);
                cell.IsTaken = true;
            }
        }

        private void SpawnTestEncounterUnits()
        {
            if (_testEncounterConfig == null)
            {
                TLog.Error("[BattleController] UseTestSetup=true but TestEncounterConfig is null. Falling back to production spawn.");
                SpawnEncounterUnits();
                return;
            }

            Transform container = UnitContainerTransform ?? transform;
            var existingUnits = FindObjectsByType<TilemapUnit>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var existing in existingUnits)
            {
                if (existing == null || existing.PlayerNumber == _humanPlayerNumber)
                    continue;

                existing.gameObject.SetActive(false);
                Destroy(existing.gameObject);
            }

            var mgr = GameAssetManager.Instance;
            var slots = _testEncounterConfig.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null)
                {
                    TLog.Warning($"[BattleController] TestEncounterSlot[{i}] is null. Skipping.");
                    continue;
                }

                if (slot.UnitPrefab == null)
                {
                    TLog.Error($"[BattleController] TestEncounterSlot[{i}] has null UnitPrefab. Skipping.");
                    continue;
                }

                if (!TryGetTestSetupCell(slot.SpawnCell, $"TestEncounterSlot[{i}]", out var cell))
                {
                    continue;
                }

                var go = Instantiate(slot.UnitPrefab, container);
                go.name = string.IsNullOrWhiteSpace(slot.DisplayName) ? $"TestEncounterUnit_{i}" : slot.DisplayName;

                var unit = go.GetComponent<TilemapUnit>();
                if (unit == null)
                {
                    TLog.Error($"[BattleController] TestEncounterSlot[{i}] prefab missing TilemapUnit.");
                    Destroy(go);
                    continue;
                }

                unit.PlayerNumber = slot.PlayerNumber;

                go.transform.position = cell.WorldPosition.ToVector3();
                unit.CurrentCell = cell;
                if (!cell.CurrentUnits.Contains(unit))
                    cell.CurrentUnits.Add(unit);
                cell.IsTaken = true;

                if (slot.PlayerNumber != _humanPlayerNumber)
                {
                    if (!string.IsNullOrWhiteSpace(slot.AiBrainAssetPath) && mgr != null)
                    {
                        var aiPath = GameAssetManager.NormalizeAssetPath(slot.AiBrainAssetPath);
                        var brain = mgr.Load<AiBrainAsset>(aiPath);
                        if (brain == null)
                        {
                            TLog.Error($"[BattleController] Test encounter AI brain not found: {aiPath}. Destroying AI unit '{go.name}'.");
                            Destroy(go);
                            continue;
                        }

                        if (!brain.IsValid())
                        {
                            TLog.Error($"[BattleController] Test encounter AI brain is invalid: {aiPath}. Destroying AI unit '{go.name}'.");
                            Destroy(go);
                            continue;
                        }

                        _loadedPaths.Add(aiPath);
                        unit.ApplyAiBrain(brain);
                    }
                    else
                    {
                        TLog.Info($"[BattleController] Test encounter unit '{go.name}' has no AiBrainAssetPath configured. Unit will have no AI.");
                    }
                }
            }
        }

        private void SpawnTestEncounterInteractables()
        {
            if (_testEncounterConfig == null) return;

            var corpseSlots = _testEncounterConfig.CorpseSlots;
            if (corpseSlots == null || corpseSlots.Count == 0) return;

            Transform container = UnitContainerTransform ?? transform;

            for (int i = 0; i < corpseSlots.Count; i++)
            {
                var slot = corpseSlots[i];
                if (slot == null)
                {
                    TLog.Warning($"[BattleController] CorpseTestSlot[{i}] is null. Skipping.");
                    continue;
                }

                if (slot.UnitPrefab == null)
                {
                    TLog.Error($"[BattleController] CorpseTestSlot[{i}] has null UnitPrefab. Skipping.");
                    continue;
                }

                if (!TryGetTestSetupCell(slot.SpawnCell, $"CorpseTestSlot[{i}]", out var cell))
                {
                    continue;
                }

                var go = Instantiate(slot.UnitPrefab, container);
                go.name = string.IsNullOrWhiteSpace(slot.DisplayName) ? $"TestCorpse_{i}" : slot.DisplayName;
                go.SetActive(true);

                var corpse = go.GetComponent<Corpse>();
                if (corpse == null)
                {
                    TLog.Error($"[BattleController] CorpseTestSlot[{i}] prefab missing Corpse component.");
                    Destroy(go);
                    continue;
                }

                go.transform.position = cell.WorldPosition.ToVector3();
                cell.AddInteractable(corpse);

                TLog.Info($"[BattleController] Corpse '{go.name}' spawned at {cell.GridCoordinates}.");
            }
        }

        private bool TryGetEncounterCell(int x, int y, out ICell cell)
        {
            cell = null;
            var manager = _cellManager ?? CellManager as UnityCellManager;
            if (manager == null)
            {
                TLog.Error("[BattleController] CellManager is null while spawning encounter units.");
                return false;
            }

            cell = manager.GetCellAt(new Vector2IntImpl(x, y));
            return cell != null;
        }

        private bool TryGetTestSetupCell(Vector2Int spawnCell, string slotLabel, out ICell cell)
        {
            cell = null;
            var manager = CellManager;
            if (manager == null)
            {
                TLog.Error($"[BattleController] CellManager is null while resolving {slotLabel}.");
                return false;
            }

            cell = manager.GetCellAt(spawnCell.ToIVector2Int());
            if (cell != null)
                return true;

            TLog.Error($"[BattleController] {slotLabel} SpawnCell '{spawnCell}' did not map to a grid cell.");
            return false;
        }

        #endregion

        #region Battle Methods

        /// <summary>
        /// 开始战斗。
        /// </summary>
        public async Task StartBattleAsync()
        {
            if (IsBattleActive || _battleStartInProgress || _isDestroying) return;

            int generation = Volatile.Read(ref _lifecycleGeneration);
            _battleStartInProgress = true;
            try
            {
                await TeardownRuntimeScopeAsync();

                if (_isDestroying || generation != Volatile.Read(ref _lifecycleGeneration))
                    return;

                var scope = new BattleRuntimeScope();
                RuntimeScope = scope;
                IsBattleActive = true;
                if (!TBattleLog.IsBattleActive)
                    TBattleLog.BeginBattle();

                Task battleUiTask = ShowBattleUIAsync(scope.Token);
                scope.Track(battleUiTask);
                Task battleConsoleTask = ShowBattleConsoleAsync(scope.Token);
                scope.Track(battleConsoleTask);

                BattleStarted?.Invoke();
            }
            finally
            {
                _battleStartInProgress = false;
            }
        }

        /// <summary>
        /// 结束战斗。
        /// </summary>
        public void EndBattle(GameResult result)
        {
            Interlocked.Increment(ref _lifecycleGeneration);

            if (!IsBattleActive)
            {
                _ = TeardownRuntimeScopeAsync();
                return;
            }

            IsBattleActive = false;
            RestoreEncounterBlockedCells();
            _ = TeardownRuntimeScopeAsync();
            BattleEnded?.Invoke(result);
            TBattleLog.EndBattle();
            UIManager.Instance.Hide(UIManager.UIId.CheatConsole);
        }

        /// <summary>
        /// 异步结束战斗，等待所有异步操作完成后再触发 BattleEnded 事件。
        /// </summary>
        public async Task EndBattleAsync(GameResult result)
        {
            Interlocked.Increment(ref _lifecycleGeneration);

            bool shouldPublish = IsBattleActive;
            if (shouldPublish)
            {
                IsBattleActive = false;
                RestoreEncounterBlockedCells();
            }

            await TeardownRuntimeScopeAsync();

            if (!shouldPublish)
                return;

            BattleEnded?.Invoke(result);
            TBattleLog.EndBattle();
            UIManager.Instance.Hide(UIManager.UIId.CheatConsole);
        }

        /// <summary>
        /// Cancels, drains, and disposes the current battle runtime scope.
        /// Repeated calls for the same scope return the same teardown task.
        /// </summary>
        public Task TeardownRuntimeScopeAsync()
        {
            IBattleRuntimeScope scope;
            TaskCompletionSource<bool> completion;

            lock (_runtimeScopeTeardownGate)
            {
                scope = RuntimeScope;
                if (scope == null)
                    return _runtimeScopeTeardownTask;

                if (ReferenceEquals(scope, _tearingDownRuntimeScope))
                    return _runtimeScopeTeardownTask;

                completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _runtimeScopeTeardownTask = completion.Task;
                _tearingDownRuntimeScope = scope;
                _runtimeScopeTeardownException = null;
            }

            _ = TeardownRuntimeScopeCoreAsync(scope, completion);
            return completion.Task;
        }

        private async Task TeardownRuntimeScopeCoreAsync(
            IBattleRuntimeScope scope,
            TaskCompletionSource<bool> completion)
        {
            try
            {
                try
                {
                    scope.Cancel();
                }
                catch (OperationCanceledException)
                {
                    // Cancellation is the expected shutdown path.
                }
                catch (Exception ex)
                {
                    RecordRuntimeScopeTeardownException(ex);
                    TLog.Error($"[BattleController] Runtime scope cancellation failed: {ex}");
                }

                try
                {
                    await scope.WhenIdleAsync();
                }
                catch (OperationCanceledException)
                {
                    // Cancellation is the expected shutdown path.
                }
                catch (Exception ex)
                {
                    RecordRuntimeScopeTeardownException(ex);
                    TLog.Error($"[BattleController] Runtime scope drain failed: {ex}");
                }
            }
            finally
            {
                try
                {
                    scope.Dispose();
                }
                catch (Exception ex)
                {
                    RecordRuntimeScopeTeardownException(ex);
                    TLog.Error($"[BattleController] Runtime scope disposal failed: {ex}");
                }
                finally
                {
                    lock (_runtimeScopeTeardownGate)
                    {
                        if (ReferenceEquals(RuntimeScope, scope))
                            RuntimeScope = null;

                        if (ReferenceEquals(_tearingDownRuntimeScope, scope))
                            _tearingDownRuntimeScope = null;
                    }

                    completion.TrySetResult(true);
                }
            }
        }

        private void RecordRuntimeScopeTeardownException(Exception exception)
        {
            lock (_runtimeScopeTeardownGate)
            {
                _runtimeScopeTeardownException = _runtimeScopeTeardownException == null
                    ? exception
                    : new AggregateException(_runtimeScopeTeardownException, exception);
            }
        }

        private async Task ShowBattleUIAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (!await WaitForGameAssetReady(cancellationToken))
                {
                    TLog.Warning("[BattleController] Battle UI skipped: GameAssetManager bootstrap did not complete in time.");
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (!IsBattleActive)
                    return;

                await UIManager.Instance.ShowAsync(UIManager.UIId.Battle);

                if (cancellationToken.IsCancellationRequested || !IsBattleActive)
                    UIManager.Instance.Hide(UIManager.UIId.Battle);
            }
            catch (OperationCanceledException)
            {
                // Battle teardown cancels startup UI work.
            }
            catch (Exception ex)
            {
                TLog.Warning($"[BattleController] Failed to show Battle UI: {ex.Message}");
            }
        }

        private async Task ShowBattleConsoleAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (!await WaitForGameAssetReady(cancellationToken))
                {
                    TLog.Warning("[BattleController] Battle console skipped: GameAssetManager bootstrap did not complete in time.");
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (!IsBattleActive)
                    return;

                await UIManager.Instance.ShowAsync(UIManager.UIId.CheatConsole);

                if (cancellationToken.IsCancellationRequested || !IsBattleActive)
                    UIManager.Instance.Hide(UIManager.UIId.CheatConsole);
            }
            catch (OperationCanceledException)
            {
                // Battle teardown cancels startup UI work.
            }
            catch (Exception ex)
            {
                TLog.Warning($"[BattleController] Failed to show battle console: {ex.Message}");
            }
        }

        private static async Task<bool> WaitForGameAssetReady(
            CancellationToken cancellationToken,
            float timeoutSeconds = 5f)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var manager = GameAssetManager.Instance;
                if (manager != null && manager.IsInitialized)
                {
                    return true;
                }

                await Awaitable.NextFrameAsync();
            }

            cancellationToken.ThrowIfCancellationRequested();
            return GameAssetManager.Instance != null && GameAssetManager.Instance.IsInitialized;
        }

        private bool ApplyEncounterBlockedCells(IReadOnlyList<BattleLayoutCell> blockedCells)
        {
            RestoreEncounterBlockedCells();
            if (blockedCells == null || blockedCells.Count == 0)
                return true;

            foreach (var blocked in blockedCells)
            {
                if (blocked == null || !TryGetEncounterCell(blocked.X, blocked.Y, out var cell))
                {
                    TLog.Error($"[BattleController] Encounter blocked cell ({blocked?.X},{blocked?.Y}) does not exist.");
                    RestoreEncounterBlockedCells();
                    return false;
                }

                _encounterBlockedCells[cell] = cell.IsTaken;
                cell.IsTaken = true;
            }

            return true;
        }

        private void RestoreEncounterBlockedCells()
        {
            foreach (var pair in _encounterBlockedCells)
            {
                if (pair.Key != null)
                    pair.Key.IsTaken = pair.Value;
            }
            _encounterBlockedCells.Clear();
        }


        /// <summary>
        /// 战斗开始时同步 HP/MP/死亡态：从地图层 CharacterDefinition 读取到战斗层 Unit。
        /// InitializeGame() 已将 MaxHealth/MaxMana 计算完毕并初始化，此处覆盖为存档状态。
        /// </summary>
        private void SyncStartingHp()
        {
            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            var friendlyUnits = GetFriendlyUnits(_humanPlayerNumber);
            foreach (var unit in friendlyUnits)
            {
                var mono = unit as MonoBehaviour;
                if (mono == null) continue;
                var link = mono.GetComponent<RosterCharacterLink>();
                if (link == null) continue;
                var def = state.Roster.FirstOrDefault(c => c.Id == link.CharacterId);
                if (def == null) continue;

                if (def.IsDead)
                {
                    unit.Health = 0f;
                    unit.IsDowned = true;
                    unit.Mana = 0f;
                    continue;
                }

                if (def.CurrentHp > 0)
                    unit.Health = Mathf.Min(def.CurrentHp, unit.MaxHealth);
                // CurrentHp == 0 且未标记死亡时，视为旧存档无 HP 记录，保留初始化值

                if (def.CurrentMp.HasValue)
                    unit.Mana = Mathf.Min(def.CurrentMp.Value, unit.MaxMana);
            }
        }

        #endregion

        #region IGridController Methods (from UnityGridController)

        public void InitializeGame(bool isNetworkInvoked = false)
        {
            _controller.InitializeGame(isNetworkInvoked);
        }

        public void StartGame(bool isNetworkInvoked = false)
        {
            _controller.StartGame(isNetworkInvoked);
        }

        public void InitializeAndStart(bool isNetworkInvoked = false)
        {
            _controller.InitializeAndStart(isNetworkInvoked);
        }

        public void EndTurn(bool isNetworkInvoked = false)
        {
            _controller.EndTurn(isNetworkInvoked);
        }

        public void MakeTurnTransition(bool isNetworkInvoked = false)
        {
            _controller.MakeTurnTransition(isNetworkInvoked);
        }

        public void InvokeGameEnded(GameResult gameResult)
        {
            _controller.InvokeGameEnded(gameResult);
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// 处理游戏初始化事件。
        /// </summary>
        private void OnGameInitialized()
        {
            GameInitialized?.Invoke();
        }

        /// <summary>
        /// 处理游戏开始事件。
        /// </summary>
        private void OnGameStarted()
        {
            GameStarted?.Invoke();
        }

        /// <summary>
        /// 处理游戏结束事件，同时触发战斗结束。
        /// </summary>
        private void OnGameEnded(GameResult result)
        {
            GameEnded?.Invoke(result);
            EndBattle(result);
        }

        /// <summary>
        /// 处理回合开始事件。
        /// </summary>
        private void OnTurnStarted(TurnTransitionParams turnTransitionParams)
        {
            if (TBattleLog.IsBattleActive)
            {
                TBattleLog.Log(new TurnLogData
                {
                    PlayerNumber = turnTransitionParams.TurnContext.CurrentPlayer?.PlayerNumber ?? -1,
                    TurnNumber = CurrentRound,
                    IsStart = true
                });
            }

            TurnStarted?.Invoke(turnTransitionParams);
        }

        /// <summary>
        /// 处理回合结束事件。
        /// </summary>
        private void OnTurnEnded(TurnTransitionParams turnTransitionParams)
        {
            if (TBattleLog.IsBattleActive)
            {
                TBattleLog.Log(new TurnLogData
                {
                    PlayerNumber = turnTransitionParams.TurnContext.CurrentPlayer?.PlayerNumber ?? -1,
                    TurnNumber = CurrentRound,
                    IsStart = false
                });
            }

            TurnEnded?.Invoke(turnTransitionParams);
        }

        /// <summary>
        /// 当单位被移除时，检查是否只剩一方的单位，若是则结束游戏。
        /// </summary>
        private void OnUnitRemoved(IUnit unit)
        {
            if (_runtimePlayers == null)
                return;

            var playersWithUnitsAlive = GetUnits()
                .Select(u => u.PlayerNumber)
                .Distinct();

            if (playersWithUnitsAlive.Count() == 1)
            {
                var winner = _runtimePlayers
                    .First(p => p.PlayerNumber == playersWithUnitsAlive.First());
                var losers = _runtimePlayers
                    .Where(p => p != winner);

                InvokeGameEnded(new GameResult(winner, losers));
            }
        }

        #endregion

        #region IPlayerManager Implementation

        /// <summary>
        /// Automatically configures _players based on units present in the scene.
        /// If _localPlayerNumber > 0 and exists in scene, that faction becomes Human.
        /// Otherwise, the faction with the fewest units becomes Human.
        /// Units with PlayerNumber <= 0 are ignored.
        /// </summary>
        private void AutoConfigurePlayersFromUnits()
        {
            if (_units == null)
            {
                TLog.Warning("[BattleController] Cannot auto-configure players: UnitManager is not initialized.");
                return;
            }

            var allUnits = GetUnits().ToList();
            if (allUnits.Count == 0)
            {
                TLog.Warning("[BattleController] No units found in scene. Cannot auto-configure players.");
                return;
            }

            var groups = allUnits
                .GroupBy(u => u.PlayerNumber)
                .Select(g => new { PlayerNumber = g.Key, Count = g.Count() })
                .OrderBy(g => g.PlayerNumber)
                .ToList();

            var entries = new List<PlayerEntry>();
            foreach (var group in groups)
            {
                int playerNumber = group.PlayerNumber;
                // PlayerNumber = 0 is human player by convention; all others are AI
                bool isHuman = (playerNumber == 0) || (_localPlayerNumber > 0 && playerNumber == _localPlayerNumber);
                entries.Add(new PlayerEntry
                {
                    PlayerNumber = playerNumber,
                    Type = isHuman ? PlayerType.HumanPlayer : PlayerType.AutomatedPlayer,
                    AITurnStartDelay = 0,
                    AIUnitDelay = 250
                });
            }

            _players = entries.ToArray();
            InitializePlayers();
            TLog.Info($"[BattleController] Auto-configured players from scene units. LocalPlayerNumber={_localPlayerNumber}. " +
                $"Config: {string.Join(", ", entries.Select(e => $"P{e.PlayerNumber}={(e.Type == PlayerType.HumanPlayer ? "Human" : "AI")}"))}");
        }

        private void InitializePlayers()
        {
            if (_players == null || _players.Length == 0)
            {
                // Fallback: create at least one human player so the game can start
                _runtimePlayers = new List<IPlayer>
                {
                    new HumanPlayer { PlayerNumber = 1 }
                };
                TLog.Warning("[BattleController] No players configured. Created fallback HumanPlayer #1.");
                return;
            }

            _runtimePlayers = new List<IPlayer>(_players.Length);
            for (int i = 0; i < _players.Length; i++)
            {
                var entry = _players[i];
                var player = entry.CreatePlayer();
                player.PlayerNumber = entry.PlayerNumber;
                // player initialized
                _runtimePlayers.Add(player);
            }
        }

        void IPlayerManager.Initialize(GridController gridController)
        {
            EnsurePlayersCoverSpawnedUnits();
            if (_runtimePlayers == null || _runtimePlayers.Count == 0)
            {
                if (_players == null || _players.Length == 0)
                {
                    AutoConfigurePlayersFromUnits(); // UnitManager is now initialized, safe to query units
                }
                else
                {
                    InitializePlayers();
                }
            }
            if (_runtimePlayers == null || _runtimePlayers.Count == 0)
            {
                TLog.Error("[BattleController] IPlayerManager.Initialize called but no players exist. Ensure _players is configured in Inspector.");
                return;
            }
            foreach (var player in _runtimePlayers)
            {
                player.Initialize(gridController);
            }
        }

        /// <summary>
        /// Encounter units are spawned immediately before player initialization. Scene
        /// player entries may therefore omit factions introduced by the encounter asset.
        /// Preserve configured player types and add every missing spawned faction as AI
        /// so turn resolution never receives a unit without an owning player.
        /// </summary>
        private void EnsurePlayersCoverSpawnedUnits()
        {
            if (_units == null)
                return;

            var existingEntries = (_players ?? Array.Empty<PlayerEntry>()).ToList();
            var existingNumbers = existingEntries
                .Select(entry => entry.PlayerNumber)
                .ToHashSet();
            bool changed = false;

            foreach (int playerNumber in _units
                .Where(unit => unit != null)
                .Select(unit => unit.PlayerNumber)
                .Distinct()
                .OrderBy(number => number))
            {
                if (!existingNumbers.Add(playerNumber))
                    continue;

                existingEntries.Add(new PlayerEntry
                {
                    PlayerNumber = playerNumber,
                    Type = playerNumber == 0 ? PlayerType.HumanPlayer : PlayerType.AutomatedPlayer,
                    AITurnStartDelay = 0,
                    AIUnitDelay = 250
                });
                changed = true;
            }

            if (!changed)
                return;

            _players = existingEntries.ToArray();
            InitializePlayers();
            TLog.Warning($"[BattleController] Added missing player entries for spawned factions: " +
                $"{string.Join(", ", existingNumbers.OrderBy(number => number))}.");
        }

        IEnumerable<IPlayer> IPlayerManager.GetPlayers()
        {
            if (_runtimePlayers == null || _runtimePlayers.Count == 0)
            {
                // Auto-initialize if not done yet
                InitializePlayers();
            }
            return _runtimePlayers;
        }

        IPlayer IPlayerManager.GetPlayerByNumber(int playerNumber)
        {
            if (_runtimePlayers == null || _runtimePlayers.Count == 0)
            {
                InitializePlayers();
            }
            if (_runtimePlayers == null || _runtimePlayers.Count == 0)
            {
                TLog.Error($"[BattleController] GetPlayerByNumber({playerNumber}) called but no players exist.");
                return null;
            }
            return _runtimePlayers.FirstOrDefault(p => p.PlayerNumber == playerNumber);
        }

        /// <summary>
        /// Helper for editor tools to quickly configure players.
        /// </summary>
        public void SetPlayers(int humanCount, int aiCount)
        {
            var entries = new List<PlayerEntry>();
            for (int i = 0; i < humanCount; i++)
            {
                entries.Add(new PlayerEntry { PlayerNumber = i + 1, Type = PlayerType.HumanPlayer });
            }
            for (int i = 0; i < aiCount; i++)
            {
                entries.Add(new PlayerEntry
                {
                    PlayerNumber = humanCount + i + 1,
                    Type = PlayerType.AutomatedPlayer,
                    AITurnStartDelay = 0,
                    AIUnitDelay = 250
                });
            }
            _players = entries.ToArray();
            InitializePlayers();
        }

        /// <summary>
        /// Configures remote players for network matches.
        /// Called by NetworkGUI.SetupMatch() to replace non-local human players with RemotePlayer instances.
        /// </summary>
        public void ConfigureRemotePlayers(int localPlayerNumber, NetworkConnection networkConnection)
        {
            if (_runtimePlayers == null) return;

            for (int i = 0; i < _runtimePlayers.Count; i++)
            {
                var player = _runtimePlayers[i];
                if (player.PlayerNumber != localPlayerNumber && player.PlayerType == PlayerType.HumanPlayer)
                {
                    _runtimePlayers[i] = new RemotePlayer
                    {
                        PlayerNumber = player.PlayerNumber,
                        NetworkConnection = networkConnection
                    };
                }
            }
        }

        #endregion

        #region IUnitManager Implementation

        public event Action<IUnit> UnitAdded;
        public event Action<IUnit> UnitRemoved;

        Transform IUnitManager.ContainerTransform => UnitContainerTransform;

        void IUnitManager.Initialize(IGridController gridController)
        {
            _units = new List<IUnit>();
            var container = _unitContainer;
            if (container == null)
            {
                var unitManagerGo = GameObject.Find("UnitManager");
                if (unitManagerGo != null)
                    container = unitManagerGo.transform;
            }
            if (container == null)
                container = transform;

            var foundUnits = container.GetComponentsInChildren<IUnit>().ToList();
            if (foundUnits.Count == 0)
            {
                TLog.Warning("[BattleController] No units found under the determined container. " +
                    "Ensure units are children of BattleController, or set _unitContainer explicitly, " +
                    "or keep a GameObject named 'UnitManager' with units as its children.");
            }

            foreach (var unit in foundUnits
                .OrderBy(u => u.CurrentCell == null)
                .ThenBy(u => u.CurrentCell?.GridCoordinates.x)
                .ThenBy(u => u.CurrentCell?.GridCoordinates.y))
            {
                AddUnit(unit);
            }
        }

        public void AddUnit(IUnit unit)
        {
            unit.UnitID = _unitCount++;
            _units.Add(unit);
            UnitAdded?.Invoke(unit);
        }

        public void RemoveUnit(IUnit unit)
        {
            // Party replacement runs before IUnitManager.Initialize creates the runtime registry.
            if (_units == null)
                return;

            _units.Remove(unit);
            UnitRemoved?.Invoke(unit);
        }

        public IEnumerable<IUnit> GetUnits()
        {
            return _units;
        }

        public IEnumerable<IUnit> GetFriendlyUnits(IPlayer player)
        {
            return GetFriendlyUnits(player.PlayerNumber);
        }

        public IEnumerable<IUnit> GetFriendlyUnits(int playerNumber)
        {
            return _units.Where(u => u.PlayerNumber == playerNumber);
        }

        public IEnumerable<IUnit> GetEnemyUnits(IPlayer player)
        {
            return GetEnemyUnits(player.PlayerNumber);
        }

        public IEnumerable<IUnit> GetEnemyUnits(int playerNumber)
        {
            return _units.Where(u => u.PlayerNumber != playerNumber);
        }

        public async Task UnMark(IEnumerable<IUnit> units)
        {
            await Task.WhenAll(units.Select(u => (u as Unit).UnMark()));
        }

        public async Task MarkAsSelected(IUnit unit)
        {
            var mono = unit as Unit;
            if (mono == null) return;
            await mono.MarkAsSelected();
        }

        public async Task MarkAsFriendly(IEnumerable<IUnit> units)
        {
            await Task.WhenAll(units.Select(u => (u as Unit)?.MarkAsFriendly() ?? Task.CompletedTask));
        }

        public async Task MarkAsFinished(IEnumerable<IUnit> units)
        {
            await Task.WhenAll(units.Select(u => (u as Unit)?.MarkAsFinished() ?? Task.CompletedTask));
        }

        public async Task MarkAsTargetable(IEnumerable<IUnit> units)
        {
            await Task.WhenAll(units.Select(u => (u as Unit).MarkAsTargetable()));
        }

        public async Task MarkAsAttacking(IUnit unit, IUnit target)
        {
            var targetUnit = target as Unit;
            await (unit as Unit).MarkAsAttacking(targetUnit);
        }

        public async Task MarkAsDefending(IUnit unit, IUnit aggressor)
        {
            var aggressorUnit = aggressor as Unit;
            await (unit as Unit).MarkAsDefending(aggressorUnit);
        }

        public async Task MarkAsMoving(IUnit unit, ICell source, ICell destination, IEnumerable<ICell> path)
        {
            await (unit as Unit).MarkAsMoving(source, destination, path);
        }

        public async Task UnMarkAsMoving(IUnit unit, ICell source, ICell destination, IEnumerable<ICell> path)
        {
            await (unit as Unit).UnMarkAsMoving(source, destination, path);
        }

        public async Task MarkAsDestroyed(IUnit unit)
        {
            await (unit as Unit).MarkAsDestroyed();
        }

        #endregion

        #region Player Entry Config

        /// <summary>
        /// Serializable player entry configured in BattleController inspector.
        /// </summary>
        [Serializable]
        public struct PlayerEntry
        {
            public int PlayerNumber;
            public PlayerType Type;

            // AI-specific settings
            [Tooltip("AI debug mode: pauses for N key between unit actions")]
            public bool AIDebugMode;
            [Tooltip("Delay (ms) before AI starts its turn")]
            public int AITurnStartDelay;
            [Tooltip("Delay (ms) between AI unit actions")]
            public int AIUnitDelay;

            public IPlayer CreatePlayer()
            {
                return Type == PlayerType.HumanPlayer
                    ? new HumanPlayer()
                    : new AIPlayer(AIDebugMode, AITurnStartDelay, AIUnitDelay);
            }
        }

        #endregion
    }
}
