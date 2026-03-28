namespace Tactics
{
    /// <summary>
    /// Clears static singletons after exiting Play Mode when domain reload is disabled.
    /// Invoked from <see cref="PlayModeStaticCleanup"/> on <see cref="UnityEditor.PlayModeStateChange.EnteredEditMode"/>.
    /// Add new <c>Instance = null</c> lines here as needed.
    /// </summary>
    public static class PlayModeStaticReset
    {
        public static void ClearAll()
        {
            Tactics.UI.RoguelikeMapUIController.Instance = null;
        }
    }
}
