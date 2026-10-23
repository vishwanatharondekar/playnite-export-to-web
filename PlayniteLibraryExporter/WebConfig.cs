namespace PlayniteLibraryExporter
{
    /// <summary>
    /// Web library service configuration. The server URL is also editable in the plugin settings,
    /// so pointing the extension at another deployment does not need a rebuild.
    /// </summary>
    public static class WebConfig
    {
        /// <summary>Default server for new installs. Change this when the site moves to its own domain.</summary>
        public const string DefaultServerUrl = "https://playnite-library.vercel.app";

        public const string PluginName = "Playnite Library Web";

        /// <summary>Delay before uploading after Playnite reports a change, so bursts of edits upload once.</summary>
        public const int LibraryUpdatedDelayMs = 5000;
        public const int GameStoppedDelayMs = 10000;
        public const int GameEditedDelayMs = 30000;

        public static string NormalizeServerUrl(string url)
        {
            var trimmed = (url ?? string.Empty).Trim().TrimEnd('/');
            return string.IsNullOrEmpty(trimmed) ? DefaultServerUrl : trimmed;
        }
    }
}
