using System;
using System.Collections.Generic;

namespace PlayniteLibraryExporter
{
    public enum ExportFormat
    {
        Json,
        Text,
        Markdown,
        Csv
    }

    public static class ExportFormatExtensions
    {
        public static string ToId(this ExportFormat format)
        {
            switch (format)
            {
                case ExportFormat.Json:
                    return "json";
                case ExportFormat.Text:
                    return "txt";
                case ExportFormat.Markdown:
                    return "markdown";
                case ExportFormat.Csv:
                    return "csv";
                default:
                    return format.ToString().ToLowerInvariant();
            }
        }

        public static string ToDisplayName(this ExportFormat format)
        {
            switch (format)
            {
                case ExportFormat.Json:
                    return "JSON";
                case ExportFormat.Text:
                    return "Plain text (.txt)";
                case ExportFormat.Markdown:
                    return "Markdown (.md)";
                case ExportFormat.Csv:
                    return "Spreadsheet CSV (.csv)";
                default:
                    return format.ToString();
            }
        }

        public static string ToExtension(this ExportFormat format)
        {
            switch (format)
            {
                case ExportFormat.Json:
                    return ".json";
                case ExportFormat.Text:
                    return ".txt";
                case ExportFormat.Markdown:
                    return ".md";
                case ExportFormat.Csv:
                    return ".csv";
                default:
                    return ".txt";
            }
        }
    }

    public class ExportSnapshot
    {
        public int DatabaseGameCount { get; set; }
        public int ExcludedGameCount { get; set; }
        public int SteamAppIdCount { get; set; }
        public string Trigger { get; set; }
        public bool Automatic { get; set; }
        public List<ExportFormat> Formats { get; set; } = new List<ExportFormat>();
        public List<GameExportDto> Games { get; set; } = new List<GameExportDto>();
    }

    public class ExportRunResult
    {
        public bool Success { get; set; }
        public string OutputDirectory { get; set; }
        public int DatabaseGameCount { get; set; }
        public int ExportedGameCount { get; set; }
        public int ExcludedGameCount { get; set; }
        public int SteamAppIdCount { get; set; }
        public List<ExportFormat> Formats { get; set; } = new List<ExportFormat>();
        public List<string> OutputPaths { get; set; } = new List<string>();
        public List<string> FailedFormats { get; set; } = new List<string>();
        public Exception Error { get; set; }
    }

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
        public List<string> formats { get; set; }
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
        public bool? isInstalled { get; set; }
        public bool? hidden { get; set; }
        public bool? favorite { get; set; }
        public string installDirectory { get; set; }
        public List<string> platforms { get; set; }
        public List<string> genres { get; set; }
        public List<string> categories { get; set; }
        public List<string> tags { get; set; }
        public string lastActivity { get; set; }
        public long? playtimeSeconds { get; set; }
        public string added { get; set; }
        public string modified { get; set; }
        public string releaseDate { get; set; }
    }
}
