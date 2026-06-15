using System;
using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Battle;
using Tactics.Common.Controllers.GameResolvers;
using Tactics.Common.Players;
using Tactics.Flow.Battle;
using Tactics.Equipment;
using Tactics.Roster;
using Tactics.Cells;
using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics.Cheats
{
    public sealed class CheatCommandManager
    {
        private static CheatCommandManager _instance;
        public static CheatCommandManager Instance => _instance ??= new CheatCommandManager();

        private readonly Dictionary<string, Func<string[], string>> _commands = new Dictionary<string, Func<string[], string>>();

        private CheatCommandManager()
        {
            RegisterBuiltInCommands();
        }

        private void RegisterBuiltInCommands()
        {
            RegisterCommand("additem", args =>
            {
                if (args.Length < 1)
                    return "[Error] Usage: additem <equipmentId>";

                string equipmentId = args[0];

                if (!EquipmentDatabase.Contains(equipmentId))
                    return $"[Error] Equipment '{equipmentId}' not found.";

                var state = PlayerAdventureStateStore.LoadRepairAndSave();
                if (state == null)
                    return "[Error] Failed to load player state.";

                if (state.Inventory == null)
                    state.Inventory = new List<string>();

                state.Inventory.Add(equipmentId);
                PlayerAdventureStateStore.Save(state);

                var def = EquipmentDatabase.GetById(equipmentId);
                return $"Added {def.DisplayName} ({equipmentId}) to inventory.";
            });

            RegisterCommand("clearitem", args =>
            {
                var state = PlayerAdventureStateStore.LoadRepairAndSave();
                if (state == null)
                    return "[Error] Failed to load player state.";

                int count = state.Inventory?.Count ?? 0;
                state.Inventory?.Clear();
                PlayerAdventureStateStore.Save(state);

                return $"Cleared {count} items from inventory.";
            });

            RegisterCommand("battle", args =>
            {
                if (args.Length < 1)
                    return "[Error] Usage: battle win [--skip] | battle lost";

                string subCommand = args[0].ToLower();
                bool skipSettlement = subCommand == "win" && args.Skip(1).Any(a => a == "--skip");

                if (BattleController.Instance == null || !BattleController.Instance.IsBattleActive)
                    return "[Error] No active battle.";

                var players = ((IPlayerManager)BattleController.Instance).GetPlayers();
                var humanPlayer = players.FirstOrDefault(p => p.PlayerType == PlayerType.HumanPlayer);
                var aiPlayers = players.Where(p => p.PlayerType == PlayerType.AutomatedPlayer).ToList();

                if (humanPlayer == null)
                    return "[Error] Human player not found.";

                if (aiPlayers.Count == 0)
                    return "[Error] AI player not found.";

                GameResult result;
                string message;

                if (subCommand == "win")
                {
                    result = new GameResult(humanPlayer, aiPlayers);
                    message = skipSettlement
                        ? "Human player wins the battle (settlement skipped)."
                        : "Human player wins the battle.";
                }
                else if (subCommand == "lost")
                {
                    result = new GameResult(aiPlayers.First(), new[] { humanPlayer });
                    message = "AI player wins the battle.";
                }
                else
                {
                    return "[Error] Usage: battle win [--skip] | battle lost";
                }

                if (skipSettlement)
                {
                    _ = BattleFlowCoordinator.Instance.EndBattleAsync(result);
                }
                else
                {
                    BattleController.Instance.EndBattle(result);
                }
                return message;
            });

            RegisterCommand("displaydebug", args =>
            {
                var cellManager = UnityEngine.Object.FindFirstObjectByType<TilemapCellManager>();
                if (cellManager == null)
                    return "[Error] TilemapCellManager not found.";

                cellManager.ShowDebugOverlay = !cellManager.ShowDebugOverlay;
                return $"Debug overlay: {(cellManager.ShowDebugOverlay ? "ON" : "OFF")}";
            });

            RegisterCommand("addexp", args =>
            {
                if (args.Length < 2)
                    return "[Error] Usage: addexp <idx> <exp_num>";

                if (!int.TryParse(args[0], out int idx))
                    return "[Error] Invalid index '" + args[0] + "'. Must be an integer.";

                if (!int.TryParse(args[1], out int expNum))
                    return "[Error] Invalid experience value '" + args[1] + "'. Must be an integer.";

                var state = PlayerAdventureStateStore.LoadRepairAndSave();
                if (state == null)
                    return "[Error] Failed to load player state.";

                if (state.Roster == null || idx < 0 || idx >= state.Roster.Count)
                    return $"[Error] Index {idx} out of range. Roster has {(state.Roster?.Count ?? 0)} characters.";

                var character = state.Roster[idx];
                int oldExp = character.Experience;
                character.Experience += expNum;

                // Track for settlement UI display
                string charId = character.Id;
                if (!BattleRewardSystem.PendingCheatExperience.ContainsKey(charId))
                    BattleRewardSystem.PendingCheatExperience[charId] = 0;
                BattleRewardSystem.PendingCheatExperience[charId] += expNum;

                PlayerAdventureStateStore.Save(state);

                TLog.Info($"[CheatCommandManager] Added {expNum} experience to {character.DisplayName}. Old: {oldExp}, New: {character.Experience}.");
                return $"Added {expNum} experience to {character.DisplayName} (idx {idx}). Old: {oldExp}, New: {character.Experience}.";
            });

            RegisterCommand("reset", args =>
            {
                var state = PlayerAdventureStateStore.LoadRepairAndSave();
                if (state == null)
                    return "[Error] Failed to load player state.";

                if (state.Roster == null || state.Roster.Count == 0)
                    return "[Error] No characters to reset.";

                int count = 0;
                for (int i = 0; i < state.Roster.Count; i++)
                {
                    var old = state.Roster[i];
                    if (old == null) continue;

                    var def = CharacterDefinition.CreateDefault(
                        old.Id,
                        old.DisplayName,
                        roleType: old.RoleType);
                    def.PrefabPath = old.PrefabPath;
                    state.Roster[i] = def;
                    count++;
                }

                if (state.Inventory != null)
                    state.Inventory.Clear();
                state.Gold = 0;

                PlayerAdventureStateStore.Save(state);
                TLog.Info($"[CheatCommandManager] Reset {count} characters, cleared inventory and gold.");
                return $"Reset {count} characters, cleared inventory and gold.";
            });
        }

        public void RegisterCommand(string name, Func<string[], string> handler)
        {
            if (string.IsNullOrWhiteSpace(name) || handler == null)
                return;

            _commands[name.ToLower()] = handler;
        }

        public string Execute(string commandLine)
        {
            if (string.IsNullOrWhiteSpace(commandLine))
                return null;

            var parts = commandLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return null;

            var name = parts[0].ToLower();
            var args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();

            if (_commands.TryGetValue(name, out var handler))
            {
                try
                {
                    return handler(args);
                }
                catch (Exception ex)
                {
                    return $"[Error] Command '{name}' failed: {ex.Message}";
                }
            }

            return $"[Error] Unknown command: '{name}'.";
        }
    }
}