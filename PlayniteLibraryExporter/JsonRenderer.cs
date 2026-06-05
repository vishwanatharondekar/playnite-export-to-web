using Playnite.SDK.Data;
using System.Collections.Generic;
using System.Linq;

namespace PlayniteLibraryExporter
{
    public class JsonRenderer
    {
        public string Render(ExportSnapshot snapshot, PlayniteLibraryExporterSettings settings, string pluginVersion)
        {
            if (!settings.IncludeExportMetadataEnvelope)
            {
                return Serialization.ToJson(snapshot.Games, settings.PrettyPrintJson);
            }

            var envelope = new ExportEnvelopeDto
            {
                schemaVersion = 1,
                generatedAtUtc = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
                generator = new GeneratorDto
                {
                    name = "Playnite Library Exporter",
                    version = pluginVersion
                },
                playnite = new PlayniteDto
                {
                    databaseGameCount = snapshot.DatabaseGameCount
                },
                export = new ExportSummaryDto
                {
                    totalGames = snapshot.Games.Count,
                    databaseGameCount = snapshot.DatabaseGameCount,
                    excludedGameCount = snapshot.ExcludedGameCount,
                    steamAppIdCount = snapshot.SteamAppIdCount,
                    formats = snapshot.Formats.Select(format => format.ToId()).ToList(),
                    automatic = snapshot.Automatic,
                    trigger = snapshot.Trigger
                },
                games = snapshot.Games
            };

            return Serialization.ToJson(envelope, settings.PrettyPrintJson);
        }
    }
}
