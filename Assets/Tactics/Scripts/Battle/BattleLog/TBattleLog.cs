using System;
using System.Collections.Generic;
using Tactics.Runtime.Utilities;

namespace Tactics.Runtime.BattleLog
{
    /// <summary>
    /// Static battle logging system. Uses TLog for output and provides
    /// battle-specific log data types.
    /// </summary>
    public static class TBattleLog
    {
        private const int MaxBattleLogEntries = 50;
        private static readonly Queue<BattleLogData> CurrentBattleLogs = new Queue<BattleLogData>(MaxBattleLogEntries);
        private static bool _battleActive;

        /// <summary>
        /// Event triggered when a battle log entry is created.
        /// Used by UI controllers to display logs.
        /// </summary>
        public static event Action<BattleLogData> OnLogToUI;

        /// <summary>
        /// Event raised after the current battle log buffer is cleared.
        /// </summary>
        public static event Action OnLogsCleared;

        /// <summary>
        /// Gets whether a battle log buffer is currently active.
        /// </summary>
        public static bool IsBattleActive => _battleActive;

        /// <summary>
        /// Whether to output battle logs to UI. Default is true.
        /// </summary>
        private static bool _outputToUI = true;

        /// <summary>
        /// Sets whether battle logs should be output to UI.
        /// </summary>
        /// <param name="outputToUI">True to output to UI, false to only output to console/file.</param>
        public static void SetOutputToUI(bool outputToUI)
        {
            _outputToUI = outputToUI;
        }

        /// <summary>
        /// Starts a new battle log scope and discards any stale entries.
        /// </summary>
        public static void BeginBattle()
        {
            _battleActive = true;
            ClearCurrentBattleLogs();
        }

        /// <summary>
        /// Ends the current battle log scope and clears its in-memory buffer.
        /// </summary>
        public static void EndBattle()
        {
            _battleActive = false;
            ClearCurrentBattleLogs();
        }

        /// <summary>
        /// Gets a stable snapshot of the current battle entries for late UI subscribers.
        /// </summary>
        public static IReadOnlyList<BattleLogData> GetCurrentBattleLogs()
        {
            return new List<BattleLogData>(CurrentBattleLogs).AsReadOnly();
        }

        /// <summary>
        /// Clears the current battle entries without affecting TLog output.
        /// </summary>
        public static void ClearCurrentBattleLogs()
        {
            CurrentBattleLogs.Clear();
            OnLogsCleared?.Invoke();
        }

        /// <summary>
        /// Logs a battle event.
        /// </summary>
        /// <param name="data">The battle log data.</param>
        public static void Log(BattleLogData data)
        {
            if (data == null)
            {
                TLog.Warning("[TBattleLog] Attempted to log null BattleLogData.");
                return;
            }

            // Output to console/file via TLog
            string displayString = data.GetDisplayString();
            TLog.Info(displayString);

            if (_battleActive)
            {
                if (CurrentBattleLogs.Count >= MaxBattleLogEntries)
                    CurrentBattleLogs.Dequeue();
                CurrentBattleLogs.Enqueue(data);
            }

            // Trigger UI event if enabled
            if (_outputToUI)
            {
                OnLogToUI?.Invoke(data);
            }
        }
    }
}
