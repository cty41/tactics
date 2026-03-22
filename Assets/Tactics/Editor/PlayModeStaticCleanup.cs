using UnityEditor;

namespace Tactics
{
    [InitializeOnLoad]
    internal static class PlayModeStaticCleanup
    {
        static PlayModeStaticCleanup()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                PlayModeStaticReset.ClearAll();
        }
    }
}
