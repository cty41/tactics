using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Interactables;
using Tactics.Common.Units;
using Tactics.Common.Utilities;
using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics.Common.Battle
{
    /// <summary>Battle-scoped Amazon spear, active movement, and decoy lifecycle state.</summary>
    public sealed class AmazonBattleState
    {
        private sealed class OwnerState
        {
            public DroppedSpear Spear;
            public IUnit Decoy;
            public int DecoyTurnsUntilExpiry;
            public int ActiveMovement;
            public Action<UnitMovedEventArgs> MovementHandler;
        }

        private sealed class DecoyMarker { }

        private static readonly ConditionalWeakTable<IGridController, AmazonBattleState> States = new();
        private static readonly ConditionalWeakTable<IUnit, DecoyMarker> Decoys = new();
        private readonly IGridController _gridController;
        private readonly Dictionary<IUnit, OwnerState> _owners = new();

        private AmazonBattleState(IGridController gridController) => _gridController = gridController;

        public static AmazonBattleState For(IGridController gridController) =>
            gridController == null ? null : States.GetValue(gridController, key => new AmazonBattleState(key));

        public static bool IsDecoy(IUnit unit) => unit != null && Decoys.TryGetValue(unit, out _);

        public bool IsSpearHeld(IUnit owner) => ResolveLiveSpear(owner) == null;
        public ICell GetSpearCell(IUnit owner) => ResolveLiveSpear(owner)?.CurrentCell;
        public int GetActiveMovement(IUnit owner) => EnsureOwner(owner).ActiveMovement;
        public void ResetActiveMovement(IUnit owner) => EnsureOwner(owner).ActiveMovement = 0;

        public bool DropSpear(IUnit owner, ICell cell)
        {
            var state = EnsureOwner(owner);
            if (owner == null || cell == null || state.Spear != null || cell.IsTaken)
                return false;

            var gameObject = new GameObject("DroppedSpear");
            if (_gridController.UnitManager?.ContainerTransform != null)
                gameObject.transform.SetParent(_gridController.UnitManager.ContainerTransform);
            gameObject.transform.position = cell.WorldPosition.ToVector3();
            var spear = gameObject.AddComponent<DroppedSpear>();
            spear.Owner = owner;
            spear.CurrentCell = cell;
            cell.AddInteractable(spear);
            state.Spear = spear;
            return true;
        }

        public bool RecoverSpear(IUnit owner)
        {
            var state = EnsureOwner(owner);
            var spear = ResolveLiveSpear(owner);
            if (spear == null)
                return false;
            spear.RemoveFromBattle();
            state.Spear = null;
            return true;
        }

        public ICell FindDropCell(IUnit owner, ICell targetCell, int radius = 3)
        {
            if (owner?.CurrentCell == null || targetCell == null || _gridController?.CellManager == null)
                return null;

            var terrainReachable = CollectTerrainReachable(owner.CurrentCell);
            int awayX = Math.Sign(targetCell.GridCoordinates.x - owner.CurrentCell.GridCoordinates.x);
            int awayY = Math.Sign(targetCell.GridCoordinates.y - owner.CurrentCell.GridCoordinates.y);
            return _gridController.CellManager.GetCells()
                .Where(cell => cell != null && !ReferenceEquals(cell, targetCell) && !cell.IsTaken)
                .Where(cell => cell.GetDistance(targetCell) <= Math.Max(1, radius))
                .Where(cell => cell.GetNeighbours(_gridController.CellManager)
                    .Any(neighbour => neighbour != null && terrainReachable.Contains(neighbour) && !IsPermanentBlocker(neighbour)))
                .OrderBy(cell => cell.GetDistance(targetCell))
                .ThenByDescending(cell =>
                    (cell.GridCoordinates.x - targetCell.GridCoordinates.x) * awayX +
                    (cell.GridCoordinates.y - targetCell.GridCoordinates.y) * awayY)
                .ThenBy(cell => cell.GridCoordinates.x)
                .ThenBy(cell => cell.GridCoordinates.y)
                .FirstOrDefault();
        }

        public void RegisterDecoy(IUnit owner, IUnit decoy, int turnsUntilExpiry)
        {
            var state = EnsureOwner(owner);
            if (state.Decoy != null && !ReferenceEquals(state.Decoy, decoy))
                DespawnDecoy(state.Decoy);
            state.Decoy = decoy;
            state.DecoyTurnsUntilExpiry = Math.Max(1, turnsUntilExpiry);
            Decoys.Remove(decoy);
            Decoys.Add(decoy, new DecoyMarker());
        }

        public IUnit GetDecoy(IUnit owner) => EnsureOwner(owner).Decoy;
        public int GetDecoyTurnsUntilExpiry(IUnit owner) => EnsureOwner(owner).DecoyTurnsUntilExpiry;

        public void OnOwnerTurnStart(IUnit owner)
        {
            var state = EnsureOwner(owner);
            if (state.Decoy == null)
                return;
            state.DecoyTurnsUntilExpiry--;
            if (state.DecoyTurnsUntilExpiry <= 0)
            {
                DespawnDecoy(state.Decoy);
                state.Decoy = null;
            }
        }

        public void OnOwnerTurnEnd(IUnit owner) => ResetActiveMovement(owner);

        public void HandleUnitDeath(IUnit unit)
        {
            if (unit == null)
                return;
            if (IsDecoy(unit))
            {
                foreach (var state in _owners.Values.Where(state => ReferenceEquals(state.Decoy, unit)))
                    state.Decoy = null;
                Decoys.Remove(unit);
                return;
            }

            if (!_owners.TryGetValue(unit, out var ownerState))
                return;
            if (ownerState.Spear != null)
                RecoverSpear(unit);
            if (ownerState.Decoy != null)
                DespawnDecoy(ownerState.Decoy);
            Detach(unit, ownerState);
            _owners.Remove(unit);
        }

        public void Clear()
        {
            foreach (var pair in _owners.ToList())
            {
                if (pair.Value.Spear != null)
                    pair.Value.Spear.RemoveFromBattle();
                if (pair.Value.Decoy != null)
                    DespawnDecoy(pair.Value.Decoy);
                Detach(pair.Key, pair.Value);
            }
            _owners.Clear();
        }

        private OwnerState EnsureOwner(IUnit owner)
        {
            if (owner == null)
                return new OwnerState();
            if (_owners.TryGetValue(owner, out var state))
                return state;

            state = new OwnerState();
            state.MovementHandler = args =>
            {
                if (ReferenceEquals(args.AffectedUnit, owner))
                    state.ActiveMovement += args.Path?.Count() ?? 0;
            };
            owner.UnitMoved += state.MovementHandler;
            _owners.Add(owner, state);
            return state;
        }

        /// <summary>
        /// Keeps the battle-state cache aligned with the live dropped-spear entity. The entity
        /// carries the ownership invariant, so a cache miss after a UI or turn transition must
        /// not make an adjacent Amazon unable to recover her still-visible spear.
        /// </summary>
        private DroppedSpear ResolveLiveSpear(IUnit owner)
        {
            if (owner == null)
                return null;

            var state = EnsureOwner(owner);
            if (state.Spear != null)
            {
                if (state.Spear.Owner == owner && state.Spear.CurrentCell != null)
                    return state.Spear;

                TLog.Warning($"[AmazonBattleState] Discarded invalid cached spear: owner={owner?.UnitID}, cachedOwner={state.Spear.Owner?.UnitID}, cell={state.Spear.CurrentCell?.GridCoordinates}.");
                state.Spear = null;
            }

            var liveSpears = UnityEngine.Object.FindObjectsByType<DroppedSpear>(FindObjectsSortMode.None)
                .Where(candidate => candidate != null && ReferenceEquals(candidate.Owner, owner) && candidate.CurrentCell != null)
                .ToList();
            if (liveSpears.Count == 0)
                return null;

            state.Spear = liveSpears[0];
            if (liveSpears.Count > 1)
            {
                TLog.Error($"[AmazonBattleState] Multiple dropped spears for owner={owner?.UnitID}; using '{state.Spear.name}' at {state.Spear.CurrentCell.GridCoordinates}.");
            }
            else
            {
                TLog.Warning($"[AmazonBattleState] Rehydrated dropped spear state: owner={owner?.UnitID}, cell={state.Spear.CurrentCell.GridCoordinates}.");
            }
            return state.Spear;
        }

        private HashSet<ICell> CollectTerrainReachable(ICell start)
        {
            var result = new HashSet<ICell> { start };
            var queue = new Queue<ICell>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var neighbour in current.GetNeighbours(_gridController.CellManager))
                {
                    if (neighbour == null || result.Contains(neighbour) || IsPermanentBlocker(neighbour))
                        continue;
                    result.Add(neighbour);
                    queue.Enqueue(neighbour);
                }
            }
            return result;
        }

        private static bool IsPermanentBlocker(ICell cell) =>
            cell.IsTaken && cell.CurrentUnits.Count == 0 && cell.CurrentInteractables.Count == 0;

        private void DespawnDecoy(IUnit decoy)
        {
            if (decoy == null)
                return;
            Decoys.Remove(decoy);
            decoy.Cleanup(_gridController);
            decoy.CurrentCell = null;
            _gridController.UnitManager?.RemoveUnit(decoy);
            decoy.OnDestroyed(_gridController);
        }

        private static void Detach(IUnit owner, OwnerState state)
        {
            if (owner != null && state?.MovementHandler != null)
                owner.UnitMoved -= state.MovementHandler;
        }
    }
}
