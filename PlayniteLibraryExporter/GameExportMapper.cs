using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PlayniteLibraryExporter
{
    /// <summary>Maps a Playnite game to the upload format. Install folders are never included.</summary>
    public static class GameExportMapper
    {
        public static GameExportDto Map(Game game)
        {
            var sourceName = game.Source?.Name;
            var steamAppId = InferSteamAppId(sourceName, game.GameId);

            return new GameExportDto
            {
                playniteId = game.Id.ToString(),
                name = game.Name,
                sortingName = NullIfWhiteSpace(game.SortingName),
                providerGameId = NullIfWhiteSpace(game.GameId),
                pluginId = game.PluginId.ToString(),
                sourceId = game.SourceId.ToString(),
                sourceName = sourceName,
                steamAppId = steamAppId,
                steamAppIdSource = steamAppId.HasValue ? "playnite-provider-game-id" : null,
                steamAppIdConfidence = steamAppId.HasValue ? "exact" : "none",
                isInstalled = game.IsInstalled,
                hidden = game.Hidden,
                favorite = game.Favorite,
                platforms = Names(game.Platforms),
                genres = Names(game.Genres),
                categories = Names(game.Categories),
                tags = Names(game.Tags),
                lastActivity = FormatDateTime(game.LastActivity),
                playtimeSeconds = Convert.ToInt64(game.Playtime),
                added = FormatDateTime(game.Added),
                modified = FormatDateTime(game.Modified),
                releaseDate = game.ReleaseDate?.Serialize()
            };
        }

        public static bool HasVisualNovelMetadata(Game game)
        {
            return HasVisualNovelName(game.Tags) || HasVisualNovelName(game.Genres);
        }

        /// <summary>Only for Steam games, whose provider game id is the Steam App ID.</summary>
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
