using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Tactics.CompileToast.Editor
{
    /// <summary>
    /// Sends Windows toast notifications via external PowerShell script.
    /// Script location: Tools/ToastNotify/ToastNotify.ps1 (relative to project root).
    /// </summary>
    internal static class WindowsCompileToastSender
    {
        private const string ScriptRelativePath = "Tools/ToastNotify/ToastNotify.ps1";

        private const string AppId = "Tactics.Unity.Editor";
        private const string ShortcutName = "Tactics Unity Editor";

        /// <summary>
        /// Ensures the Start Menu shortcut with proper AppUserModelID exists.
        /// Must be called once before sending any toast notifications.
        /// </summary>
        internal static void EnsureAppIdRegistered()
        {
#if UNITY_EDITOR_WIN
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var ensurePath = Path.Combine(projectRoot, "Tools", "ToastNotify", "Ensure-ToastAppId.ps1");

            if (!File.Exists(ensurePath))
            {
                Debug.LogWarning($"[CompileToast] Ensure-ToastAppId.ps1 not found at: {ensurePath}");
                return;
            }

            var unityPath = EditorApplication.applicationPath;
            var unityDir = Path.GetDirectoryName(unityPath);
            var args = $"-ExecutionPolicy Bypass -File \"{ensurePath}\" " +
                       $"-AppId \"{AppId}\" " +
                       $"-ShortcutName \"{ShortcutName}\" " +
                       $"-TargetPath \"{unityPath}\" " +
                       $"-WorkingDirectory \"{unityDir}\"";

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            try
            {
                using var process = new Process { StartInfo = startInfo };
                process.Start();
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CompileToast] Failed to ensure AppId registration: {ex.Message}");
            }
#endif
        }

        internal static void TrySend(string title, string message)
        {
#if UNITY_EDITOR_WIN
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var toastPath = Path.Combine(projectRoot, ScriptRelativePath);

            if (!File.Exists(toastPath))
            {
                Debug.LogWarning($"[CompileToast] ToastNotify.ps1 not found at: {toastPath}");
                return;
            }

            // Escape arguments for PowerShell
            var titleEsc = EscapeArg(title);
            var msgEsc = EscapeArg(message);

            var args = $"-ExecutionPolicy Bypass -File \"{toastPath}\" " +
                       $"-AppId \"{AppId}\" " +
                       $"-Title {titleEsc} " +
                       $"-Message {msgEsc}";

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            try
            {
                using var process = new Process { StartInfo = startInfo };
                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(error))
                    Debug.LogError($"[CompileToast] {error.Trim()}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CompileToast] Failed to launch toast: {ex.Message}");
            }
#endif
        }

        /// <summary>
        /// Escapes a string for safe passing to PowerShell as a single argument.
        /// Wraps in double quotes and escapes internal double quotes/backslashes
        /// to satisfy Windows CommandLineToArgvW rules.
        /// </summary>
        private static string EscapeArg(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"\"";

            var sb = new StringBuilder();
            sb.Append('\"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '"')
                {
                    sb.Append('\\');
                    sb.Append('\"');
                }
                else if (c == '\\')
                {
                    int backslashCount = 1;
                    while (i + 1 < value.Length && value[i + 1] == '\\')
                    {
                        backslashCount++;
                        i++;
                    }
                    if (i + 1 < value.Length && value[i + 1] == '"')
                    {
                        sb.Append('\\', backslashCount * 2);
                    }
                    else
                    {
                        sb.Append('\\', backslashCount);
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            sb.Append('\"');
            return sb.ToString();
        }
    }
}
