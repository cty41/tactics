using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Tactics.CompileToast.Editor
{
    [InitializeOnLoad]
    internal static class CompilationFinishedToastHook
    {
        private static DateTime _compileStartTime;

        private static bool _appIdRegistered;

        static CompilationFinishedToastHook()
        {
#if UNITY_EDITOR_WIN
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
#endif
        }

#if UNITY_EDITOR_WIN
        private static void OnCompilationStarted(object _)
        {
            _compileStartTime = DateTime.UtcNow;
        }

        private static void OnCompilationFinished(object _)
        {
            // Ensure AppId is registered once (lazy init on first compile)
            if (!_appIdRegistered)
            {
                WindowsCompileToastSender.EnsureAppIdRegistered();
                _appIdRegistered = true;
            }

            EditorApplication.delayCall += SendToastIfSucceeded;
        }

        private static void SendToastIfSucceeded()
        {
            var elapsed = DateTime.UtcNow - _compileStartTime;

            if (EditorUtility.scriptCompilationFailed)
            {
                WindowsCompileToastSender.TrySend(
                    "Tactics",
                    $"编译失败\n耗时 {elapsed:mm\\:ss}");
                return;
            }

            var (errors, warnings) = GetCompileStats();
            var summary = errors > 0 ? $"错误: {errors}" : "成功";
            var details = new List<string> { summary };

            if (warnings > 0)
                details.Add($"警告: {warnings}");

            details.Add($"耗时: {elapsed:mm\\:ss}");

            WindowsCompileToastSender.TrySend("Tactics", string.Join("\n", details));
        }

        private static (int errors, int warnings) GetCompileStats()
        {
            // Use LogEntries API if available (Unity 2021.2+)
            var errors = 0;
            var warnings = 0;

            try
            {
                var logEntries = Type.GetType("UnityEditor.LogEntries,UnityEditor");
                if (logEntries != null)
                {
                    var getCount = logEntries.GetMethod("GetCount");
                    if (getCount != null)
                    {
                        var count = (int)getCount.Invoke(null, null);
                        var getEntry = logEntries.GetMethod("GetEntryAt");
                        var entryType = Type.GetType("UnityEditor.LogEntry,UnityEditor");

                        for (int i = 0; i < count; i++)
                        {
                            var entry = Activator.CreateInstance(entryType);
                            getEntry?.Invoke(null, new object[] { i, entry });
                            var modeField = entryType.GetField("mode");
                            var modeValue = modeField?.GetValue(entry) as int?;

                            // mode bit 1 = error, bit 2 = warning
                            if (modeValue.HasValue)
                            {
                                if ((modeValue.Value & 1) != 0) errors++;
                                if ((modeValue.Value & 2) != 0) warnings++;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fall back: unknown stats, just report success
            }

            return (errors, warnings);
        }
#endif
    }
}
