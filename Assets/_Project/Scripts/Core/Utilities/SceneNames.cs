namespace ChibiRift.Core
{
    /// <summary>
    /// The five scenes of SRS 27, as constants so no scene name is ever typed as a literal
    /// at a call site. Values must match the file names under Assets/_Project/Scenes and the
    /// entries in Build Settings.
    /// </summary>
    public static class SceneNames
    {
        /// <summary>Entry point. Builds the ServiceLocator, then loads <see cref="MainMenu"/>.</summary>
        public const string Boot = "Boot";

        /// <summary>Play / Settings / How to Play / Quit. No Continue entry (SAVE-006).</summary>
        public const string MainMenu = "MainMenu";

        /// <summary>Hero selection and permanent upgrades (HUB-001..HUB-003).</summary>
        public const string Hub = "Hub";

        /// <summary>The MVP stage: waves then boss (SRS 14, SRS 15).</summary>
        public const string Run01 = "Run_01";

        /// <summary>Run summary and the single reward commit (RUN-004, RUN-005, RUN-006).</summary>
        public const string PostRun = "PostRun";

        /// <summary>Every scene, in build order.</summary>
        public static readonly string[] All = { Boot, MainMenu, Hub, Run01, PostRun };
    }
}
