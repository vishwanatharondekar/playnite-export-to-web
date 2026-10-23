using System.Collections.Generic;

namespace PlayniteLibraryExporter
{
    /// <summary>Upload body for PUT /api/me/export (export schema version 1).</summary>
    public class ExportEnvelopeDto
    {
        public int schemaVersion { get; set; }
        public string generatedAtUtc { get; set; }
        public GeneratorDto generator { get; set; }
        public PlayniteDto playnite { get; set; }
        public ExportSummaryDto export { get; set; }
        public List<GameExportDto> games { get; set; }
    }

    public class GeneratorDto
    {
        public string name { get; set; }
        public string version { get; set; }
    }

    public class PlayniteDto
    {
        public int databaseGameCount { get; set; }
    }

    public class ExportSummaryDto
    {
        public int totalGames { get; set; }
        public int databaseGameCount { get; set; }
        public int excludedGameCount { get; set; }
        public int steamAppIdCount { get; set; }
        public bool automatic { get; set; }
        public string trigger { get; set; }
    }

    public class GameExportDto
    {
        public string playniteId { get; set; }
        public string name { get; set; }
        public string sortingName { get; set; }
        public string providerGameId { get; set; }
        public string pluginId { get; set; }
        public string sourceId { get; set; }
        public string sourceName { get; set; }
        public int? steamAppId { get; set; }
        public string steamAppIdSource { get; set; }
        public string steamAppIdConfidence { get; set; }
        public bool isInstalled { get; set; }
        public bool hidden { get; set; }
        public bool favorite { get; set; }
        public List<string> platforms { get; set; }
        public List<string> genres { get; set; }
        public List<string> categories { get; set; }
        public List<string> tags { get; set; }
        public string lastActivity { get; set; }
        public long playtimeSeconds { get; set; }
        public string added { get; set; }
        public string modified { get; set; }
        public string releaseDate { get; set; }
    }
}
