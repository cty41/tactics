using System;
using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Battle;
using Tactics.Common.Controllers.GameResolvers;
using Tactics.Common.Players;
using Tactics.Flow.Battle;
using Tactics.Equipment;
using Tactics.Roster;
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