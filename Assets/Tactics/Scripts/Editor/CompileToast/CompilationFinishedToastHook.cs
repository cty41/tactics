using UnityEditor;
using UnityEditor.Compilation;

namespace Tactics.CompileToast.Editor
{
    [InitializeOnLoad]
    internal static class CompilationFinishedToastHook
    {
        static CompilationFinishedToastHook()
        {
#if UNITY_EDITOR_WIN
            CompilationPipeline.compilationFinished += OnCompilationFinished;
#endif
        }

#if UNITY_EDITOR_WIN
        private static void OnCompilationFinished(object _)
        {
            EditorApplication.delayCall += SendToastIfSucceeded;
        }

        private static void SendToastIfSucceeded()
        {
            if (EditorUtility.scriptCompilationFailed)
                return;

            WindowsCompileToastSender.TrySend("Tactics", "脚本编译完成");
        }
#endif
    }
}
