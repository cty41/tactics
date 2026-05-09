using System;
using Tactics.Runtime.Utilities;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Tactics.AssetPipeline;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Controllers.GameResolvers;
using Tactics.Common.Controllers.GridStates;
using Tactics.Common.Controllers.TurnResolvers;
using Tactics.Common.Network;
using Tactics.Common.Players;
using Tactics.Common.Units;
using Tactics.Common.Units.Classes;
using Tactics.Common.Utilities;
using Tactics.Roguelike;
using Tactics.Roster;
using Tactics.UI;
using Tactics.Units;

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

        #endregion

        #region Private Fields

        private readonly GridController _controller = new GridController();
        private IList<IPlayer> _runtimePlayers;
        private IList<IUnit> _units;
        private int _unitCount;
        private readonly HashSet<string> _loadedPaths = new();

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
            _controller.GameInitialized -= OnGameInitialized;
            _controller.GameEnded -= OnGameEnded;
            _controller.GameStarted -= OnGameStarted;
            _controller.TurnStarted -= OnTurnStarted;
            _controller.TurnEnded -= OnTurnEnded;

            UnitRemoved -= OnUnitRemoved;

            var mgr = GameAssetManager.Instance;
            if (mgr != null)
            {
                foreach (var path in _loadedPaths)
                    mgr.Release(path);
            }
            _loadedPaths.Clear();

            RoguelikeBattleReturnHandler.Instance.UnregisterController(this);
            base.OnDestroy();
        }

        private IEnumerator Start()
        {
            if (this == null || gameObject == null) yield break;

            while (GameAssetManager.Instance == null || !GameAssetManager.Instance.IsInitialized)
                yield return null;

            SpawnPartyUnits();

            if (_startImmediatelly)
            {
                InitializeGame();
                StartGame();
                _ = StartBattleAsync();
            }
        }

        private void SpawnPartyUnits()
        {
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

                if (i < respawnPoints.Count)
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

                var link = unit.GetComponent<RosterCharacterLink>();
                if (link == null)
                    link = unit.gameObject.AddComponent<RosterCharacterLink>();
                link.CharacterId = def.Id;
            }
        }

        #endregion

        #region Battle Methods

        /// <summary>
        /// 开始战斗。
        /// </summary>
        public async Task StartBattleAsync()
        {
            if (IsBattleActive) return;
            IsBattleActive = true;

            _ = ShowBattleUIAsync();

            BattleStarted?.Invoke();
        }

        /// <summary>
        /// 结束战斗。
        /// </summary>
        public void EndBattle(GameResult result)
        {
            if (!IsBattleActive) return;
            IsBattleActive = false;
            BattleEnded?.Invoke(result);
        }

        private async Task ShowBattleUIAsync()
        {
            try
            {
                await UIManager.Instance.ShowAsync(UIManager.UIId.Battle);
            }
            catch (Exception ex)
            {
                TLog.Warning($"[BattleController] Failed to show Battle UI: {ex.Message}");
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
            TurnStarted?.Invoke(turnTransitionParams);
        }

        /// <summary>
        /// 处理回合结束事件。
        /// </summary>
        private void OnTurnEnded(TurnTransitionParams turnTransitionParams)
        {
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
            await (unit as Unit).MarkAsSelected();
        }

        public async Task MarkAsFriendly(IEnumerable<IUnit> units)
        {
            await Task.WhenAll(units.Select(u => (u as Unit).MarkAsFriendly()));
        }

        public async Task MarkAsFinished(IEnumerable<IUnit> units)
        {
            await Task.WhenAll(units.Select(u => (u as Unit).MarkAsFinished()));
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