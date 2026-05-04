using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Tactics.CompileToast.Editor
{
    [InitializeOnLoad]
    internal static class CompilationFinishedToastHook
    {
        private const string PendingKey = "CompileToast_Pending";
        private const string FailedKey = "CompileToast_Failed";
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
            if (!_appIdRegistered)
            {
                WindowsCompileToastSender.EnsureAppIdRegistered();
                _appIdRegistered = true;
            }
        }

        private static void OnCompilationFinished(object _)
        {
            SessionState.SetBool(PendingKey, true);
            SessionState.SetBool(FailedKey, EditorUtility.scriptCompilationFailed);
        }

        [InitializeOnLoadMethod]
        private static void CheckPendingToast()
        {
            if (!SessionState.GetBool(PendingKey, false))
                return;

            SessionState.EraseBool(PendingKey);
            bool failed = SessionState.GetBool(FailedKey, false);
            SessionState.EraseBool(FailedKey);

            if (failed)
                WindowsCompileToastSender.TrySend("Tactics", "编译失败");
            else
                WindowsCompileToastSender.TrySend("Tactics", "编译成功");
        }
#endif
    }
}
