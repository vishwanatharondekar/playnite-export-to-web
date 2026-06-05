using System.Linq;
using System.Text;

namespace PlayniteLibraryExporter
{
    public class TextRenderer
    {
        public string Render(ExportSnapshot snapshot)
        {
            var builder = new StringBuilder();
            foreach (var game in snapshot.Games)
            {
                builder.Append(game.name);
                if (game.steamAppId.HasValue)
                {
                    builder.Append(" [Steam AppID: ");
                    builder.Append(game.steamAppId.Value);
                    builder.Append("]");
                }

                if (game.tags != null && game.tags.Any())
                {
                    builder.Append(" [Tags: ");
                    builder.Append(string.Join(", ", game.tags));
                    builder.Append("]");
                }

                builder.AppendLine();
            }

            return builder.ToString();
        }
    }
}
