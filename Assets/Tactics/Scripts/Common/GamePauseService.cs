namespace Tactics
{
    /// <summary>
    /// Backward-compatible pause API. GameTimeService owns pause state and Time.timeScale.
    /// </summary>
    /// <remarks>
    /// New gameplay code should use <see cref="GameTimeService"/> directly. This facade does not
    /// keep independent state, so legacy pause calls compose with playback speed and nested pauses.
    /// </remarks>
    public static class GamePauseService
    {
        /// <summary>Gets whether at least one pause owner is active.</summary>
        public static bool IsPaused => GameTimeService.IsPaused;

        /// <summary>Adds one pause owner.</summary>
        public static void Pause() => GameTimeService.Pause();

        /// <summary>Releases one pause owner without underflowing the pause depth.</summary>
        public static void Resume() => GameTimeService.Resume();

        /// <summary>Clears every pause owner and restores the selected playback speed.</summary>
        public static void ForceResume() => GameTimeService.ForceResume();
    }
}
