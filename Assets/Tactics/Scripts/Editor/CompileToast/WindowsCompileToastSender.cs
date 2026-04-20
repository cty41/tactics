using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Tactics.CompileToast.Editor
{
    internal static class WindowsCompileToastSender
    {
        private const string RelativeRoot = "Tactics/Scripts/Editor/CompileToast/Plugins";

        internal static void TrySend(string title, string content)
        {
#if UNITY_EDITOR_WIN
            var scriptPath = Path.GetFullPath(Path.Combine(Application.dataPath, RelativeRoot, "ToastSenderTool.ps1"));
            var dllPath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                RelativeRoot,
                "dll",
                Environment.Is64BitProcess ? "x64" : "x86",
                "System.Runtime.WindowsRuntime.dll"));

            if (!File.Exists(scriptPath) || !File.Exists(dllPath))
            {
                Debug.LogWarning("[CompileToast] ToastSenderTool.ps1 or System.Runtime.WindowsRuntime.dll missing.");
                return;
            }

            var appId = EditorApplication.applicationPath;

            var args = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" " +
                       $"-dllPath \"{dllPath}\" " +
                       $"-appId \"{appId}\" " +
                       $"-title \"{title}\" " +
                       $"-content \"{content}\"";

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            try
            {
                using var process = new Process { StartInfo = startInfo };
                process.Start();
                process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (!string.IsNullOrEmpty(error))
                    Debug.LogError($"[CompileToast] {error}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CompileToast] Failed to launch toast: {ex.Message}");
            }
#endif
        }
    }
}
