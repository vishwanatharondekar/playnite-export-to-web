using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PlayniteLibraryExporter
{
    public class CsvRenderer
    {
        private static readonly string[] Headers =
        {
            "playniteId",
            "name",
            "sortingName",
            "providerGameId",
            "pluginId",
            "sourceId",
            "sourceName",
            "steamAppId",
            "steamAppIdSource",
            "steamAppIdConfidence",
            "isInstalled",
            "hidden",
            "favorite",
            "tags"
        };

        public string Render(ExportSnapshot snapshot)
        {
            var builder = new StringBuilder();
            AppendRow(builder, Headers);

            foreach (var game in snapshot.Games)
            {
                AppendRow(builder, new[]
                {
                    game.playniteId,
                    game.name,
                    game.sortingName,
                    game.providerGameId,
                    game.pluginId,
                    game.sourceId,
                    game.sourceName,
                    game.steamAppId?.ToString(),
                    game.steamAppIdSource,
                    game.steamAppIdConfidence,
                    game.isInstalled?.ToString(),
                    game.hidden?.ToString(),
                    game.favorite?.ToString(),
                    game.tags == null ? string.Empty : string.Join("; ", game.tags)
                });
            }

            return builder.ToString();
        }

        private static void AppendRow(StringBuilder builder, IEnumerable<string> values)
        {
            builder.AppendLine(string.Join(",", values.Select(Escape)));
        }

        private static string Escape(string value)
        {
            value = value ?? string.Empty;
            var mustQuote = value.Contains(",") || value.Contains("\"") || value.Contains("\r") || value.Contains("\n");
            value = value.Replace("\"", "\"\"");
            return mustQuote ? $"\"{value}\"" : value;
        }
    }
}
