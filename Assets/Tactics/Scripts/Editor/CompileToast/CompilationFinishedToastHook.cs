using System;
using UnityEditor;
using UnityEditor.Compilation;

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
            if (EditorUtility.scriptCompilationFailed)
            {
                WindowsCompileToastSender.TrySend("Tactics", "编译失败");
                return;
            }

            WindowsCompileToastSender.TrySend("Tactics", "编译成功");
        }
#endif
    }
}
