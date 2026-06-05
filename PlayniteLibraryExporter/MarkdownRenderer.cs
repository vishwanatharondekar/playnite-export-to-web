using System.Linq;
using System.Text;

namespace PlayniteLibraryExporter
{
    public class MarkdownRenderer
    {
        public string Render(ExportSnapshot snapshot)
        {
            var builder = new StringBuilder();
            builder.AppendLine("# Playnite Library Export");
            builder.AppendLine();
            builder.AppendLine($"Generated games: {snapshot.Games.Count}");
            builder.AppendLine($"Database games: {snapshot.DatabaseGameCount}");
            builder.AppendLine($"Excluded by filters: {snapshot.ExcludedGameCount}");
            builder.AppendLine($"Steam AppIDs found: {snapshot.SteamAppIdCount}");
            builder.AppendLine($"Formats: {string.Join(", ", snapshot.Formats.Select(format => format.ToId()))}");
            builder.AppendLine();
            builder.AppendLine("| Name | Source | Steam AppID | Tags |");
            builder.AppendLine("| --- | --- | --- | --- |");

            foreach (var game in snapshot.Games)
            {
                builder.Append("| ");
                builder.Append(Escape(game.name));
                builder.Append(" | ");
                builder.Append(Escape(game.sourceName));
                builder.Append(" | ");
                builder.Append(game.steamAppId?.ToString() ?? string.Empty);
                builder.Append(" | ");
                builder.Append(Escape(game.tags == null ? string.Empty : string.Join(", ", game.tags)));
                builder.AppendLine(" |");
            }

            return builder.ToString();
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("|", "\\|")
                .Replace("\r", " ")
                .Replace("\n", " ");
        }
    }
}
