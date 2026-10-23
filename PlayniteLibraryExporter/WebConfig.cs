namespace PlayniteLibraryExporter
{
    /// <summary>Web library service configuration.</summary>
    public static class WebConfig
    {
        /// <summary>
        /// The web library server. Change this when the site moves to its own domain; existing links keep
        /// working because device tokens belong to the account, not to the domain.
        /// </summary>
        public const string ServerUrl = "https://playnite-library.vercel.app";

        public const string PluginName = "Playnite Library Web";

        /// <summary>Delay before uploading after Playnite reports a change, so bursts of edits upload once.</summary>
        public const int LibraryUpdatedDelayMs = 5000;
        public const int GameStoppedDelayMs = 10000;
        public const int GameEditedDelayMs = 30000;
    }
}
