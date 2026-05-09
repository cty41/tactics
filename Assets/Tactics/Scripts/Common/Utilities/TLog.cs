using System;
using System.IO;
using UnityEngine;

namespace Tactics.Runtime.Utilities
{
    /// <summary>
    /// Static universal logging system. Replaces Unity's native Debug.Log.
    /// Supports Console output, file output, and log level filtering.
    /// </summary>
    public static class TLog
    {
        private static LogLevel _minLogLevel = LogLevel.Info;
        private static bool _enableConsole = true;
        private static bool _enableFile = false;
        private static string _logFilePath = "";
        private static bool _includeTimestamp = true;
        private static bool _includeContext = true;
        private static readonly object _lock = new object();

        /// <summary>
        /// Gets the log file path.
        /// </summary>
        public static string LogFilePath => _logFilePath;

        /// <summary>
        /// Gets whether console output is enabled.
        /// </summary>
        public static bool EnableConsole => _enableConsole;

        /// <summary>
        /// Gets whether file output is enabled.
        /// </summary>
        public static bool EnableFile => _enableFile;

        /// <summary>
        /// Gets the minimum log level.
        /// </summary>
        public static LogLevel MinLogLevel => _minLogLevel;

        /// <summary>
        /// Sets the minimum log level.
        /// </summary>
        /// <param name="level">The minimum log level.</param>
        public static void SetLogLevel(LogLevel level)
        {
            _minLogLevel = level;
        }

        /// <summary>
        /// Enables file output to the specified path.
        /// </summary>
        /// <param name="path">The log file path.</param>
        public static void EnableFileOutput(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[TLog] Log file path cannot be null or empty.");
                return;
            }

            _logFilePath = path;
            _enableFile = true;

            // Ensure directory exists
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        /// <summary>
        /// Disables file output.
        /// </summary>
        public static void DisableFileOutput()
        {
            _enableFile = false;
            _logFilePath = "";
        }

        /// <summary>
        /// Enables or disables console output.
        /// </summary>
        /// <param name="enable">True to enable console output, false to disable.</param>
        public static void EnableConsoleOutput(bool enable)
        {
            _enableConsole = enable;
        }

        /// <summary>
        /// Logs an info message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="context">The context object (optional).</param>
        public static void Info(string message, object context = null)
        {
            Log(LogLevel.Info, message, context);
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="context">The context object (optional).</param>
        public static void Warning(string message, object context = null)
        {
            Log(LogLevel.Warning, message, context);
        }

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="context">The context object (optional).</param>
        public static void Error(string message, object context = null)
        {
            Log(LogLevel.Error, message, context);
        }

        /// <summary>
        /// Internal log method.
        /// </summary>
        private static void Log(LogLevel level, string message, object context)
        {
            if (level < _minLogLevel)
                return;

            string timestamp = _includeTimestamp ? $"[{DateTime.Now:HH:mm:ss}] " : "";
            string contextStr = (_includeContext && context != null) ? $" ({context.GetType().Name})" : "";
            string fullMessage = $"{timestamp}[{level.ToString().ToUpper()}] {message}{contextStr}";

            // Output to console
            if (_enableConsole)
            {
                switch (level)
                {
                    case LogLevel.Info:
                        Debug.Log(fullMessage);
                        break;
                    case LogLevel.Warning:
                        Debug.LogWarning(fullMessage);
                        break;
                    case LogLevel.Error:
                        Debug.LogError(fullMessage);
                        break;
                }
            }

            // Output to file
            if (_enableFile && !string.IsNullOrEmpty(_logFilePath))
            {
                WriteToFile(fullMessage);
            }
        }

        /// <summary>
        /// Writes a message to the log file.
        /// </summary>
        private static void WriteToFile(string message)
        {
            lock (_lock)
            {
                try
                {
                    File.AppendAllText(_logFilePath, message + Environment.NewLine);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[TLog] Failed to write to log file: {e.Message}");
                }
            }
        }
    }
}
