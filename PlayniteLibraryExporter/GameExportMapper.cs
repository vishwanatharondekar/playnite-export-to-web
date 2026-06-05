using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PlayniteLibraryExporter
{
    public class GameExportMapper
    {
        private readonly bool includeOnlyCoreFields;
        private readonly bool includeLocalInstallPaths;

        public GameExportMapper(bool includeOnlyCoreFields, bool includeLocalInstallPaths)
        {
            this.includeOnlyCoreFields = includeOnlyCoreFields;
            this.includeLocalInstallPaths = includeLocalInstallPaths;
        }

        public GameExportDto Map(Game game)
        {
            var sourceName = game.Source?.Name;
            var steamAppId = InferSteamAppId(sourceName, game.GameId);

            var dto = new GameExportDto
            {
                playniteId = game.Id.ToString(),
                name = game.Name,
                sortingName = string.IsNullOrWhiteSpace(game.SortingName) ? null : game.SortingName,
                providerGameId = string.IsNullOrWhiteSpace(game.GameId) ? null : game.GameId,
                pluginId = game.PluginId.ToString(),
                sourceId = game.SourceId.ToString(),
                sourceName = sourceName,
                steamAppId = steamAppId,
                steamAppIdSource = steamAppId.HasValue ? "playnite-provider-game-id" : null,
                steamAppIdConfidence = steamAppId.HasValue ? "exact" : "none",
                tags = Names(game.Tags)
            };

            if (!includeOnlyCoreFields)
            {
                dto.isInstalled = game.IsInstalled;
                dto.hidden = game.Hidden;
                dto.favorite = game.Favorite;
                dto.installDirectory = includeLocalInstallPaths ? NullIfWhiteSpace(game.InstallDirectory) : null;
                dto.platforms = Names(game.Platforms);
                dto.genres = Names(game.Genres);
                dto.categories = Names(game.Categories);
                dto.lastActivity = FormatDateTime(game.LastActivity);
                dto.playtimeSeconds = Convert.ToInt64(game.Playtime);
                dto.added = FormatDateTime(game.Added);
                dto.modified = FormatDateTime(game.Modified);
                dto.releaseDate = game.ReleaseDate?.Serialize();
            }

            return dto;
        }

        public static bool HasVisualNovelMetadata(Game game)
        {
            return HasVisualNovelName(game.Tags) || HasVisualNovelName(game.Genres);
        }

        private static int? InferSteamAppId(string sourceName, string providerGameId)
        {
            if (!string.Equals(sourceName?.Trim(), "Steam", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (int.TryParse(providerGameId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0)
            {
                return id;
            }

            return null;
        }

        private static List<string> Names(IEnumerable<DatabaseObject> values)
        {
            return values?
                .Where(value => !string.IsNullOrWhiteSpace(value?.Name))
                .Select(value => value.Name)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();
        }

        private static bool HasVisualNovelName(IEnumerable<DatabaseObject> values)
        {
            return values != null &&
                   values.Any(value => string.Equals(value?.Name?.Trim(), "visual novel", StringComparison.OrdinalIgnoreCase));
        }

        private static string NullIfWhiteSpace(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static string FormatDateTime(DateTime? value)
        {
            return value?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        }
    }
}
