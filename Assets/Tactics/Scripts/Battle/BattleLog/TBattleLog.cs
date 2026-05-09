using System;
using Tactics.Runtime.Utilities;

namespace Tactics.Runtime.BattleLog
{
    /// <summary>
    /// Static battle logging system. Uses TLog for output and provides
    /// battle-specific log data types.
    /// </summary>
    public static class TBattleLog
    {
        /// <summary>
        /// Event triggered when a battle log entry is created.
        /// Used by UI controllers to display logs.
        /// </summary>
        public static event Action<BattleLogData> OnLogToUI;

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

            // Trigger UI event if enabled
            if (_outputToUI)
            {
                OnLogToUI?.Invoke(data);
            }
        }
    }
}
