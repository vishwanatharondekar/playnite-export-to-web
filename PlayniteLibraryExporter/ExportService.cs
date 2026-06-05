using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PlayniteLibraryExporter
{
    public class ExportService
    {
        private readonly IPlayniteAPI playniteApi;
        private readonly Func<PlayniteLibraryExporterSettings> getSettings;
        private readonly ILogger logger;
        private readonly SemaphoreSlim exportLock = new SemaphoreSlim(1, 1);
        private readonly ExportFileWriter fileWriter = new ExportFileWriter();
        private readonly JsonRenderer jsonRenderer = new JsonRenderer();
        private readonly TextRenderer textRenderer = new TextRenderer();
        private readonly MarkdownRenderer markdownRenderer = new MarkdownRenderer();
        private readonly CsvRenderer csvRenderer = new CsvRenderer();

        public ExportService(IPlayniteAPI playniteApi, Func<PlayniteLibraryExporterSettings> getSettings, ILogger logger)
        {
            this.playniteApi = playniteApi;
            this.getSettings = getSettings;
            this.logger = logger;
        }

        public async Task<ExportRunResult> ExportAsync(string trigger, bool automatic)
        {
            await exportLock.WaitAsync();
            try
            {
                var settings = getSettings();
                settings.Normalize();

                var errors = PlayniteLibraryExporterSettingsViewModel.Validate(settings);
                if (errors.Any())
                {
                    throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
                }

                logger.Info($"Starting Playnite library export. Trigger={trigger}, Automatic={automatic}, Formats={string.Join(",", settings.GetSelectedFormats().Select(format => format.ToId()))}");

                var snapshot = await CaptureSnapshotAsync(settings, trigger, automatic);
                var result = await Task.Run(() => RenderAndWrite(settings, snapshot));

                logger.Info($"Playnite library export completed. OutputDirectory={result.OutputDirectory}, Formats={string.Join(",", result.Formats.Select(format => format.ToId()))}, Games={result.ExportedGameCount}, Excluded={result.ExcludedGameCount}, SteamAppIds={result.SteamAppIdCount}");
                return result;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Playnite library export failed. Trigger={trigger}, Automatic={automatic}");
                return new ExportRunResult
                {
                    Success = false,
                    Error = ex
                };
            }
            finally
            {
                exportLock.Release();
            }
        }

        public bool AnyConfiguredOutputExists()
        {
            var settings = getSettings();
            settings.Normalize();
            return settings.GetSelectedFormats().Any(format =>
                File.Exists(Path.Combine(settings.OutputDirectory, settings.OutputFileBaseName + format.ToExtension())));
        }

        private Task<ExportSnapshot> CaptureSnapshotAsync(PlayniteLibraryExporterSettings settings, string trigger, bool automatic)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                return dispatcher.InvokeAsync(() => CaptureSnapshot(settings, trigger, automatic)).Task;
            }

            return Task.FromResult(CaptureSnapshot(settings, trigger, automatic));
        }

        private ExportSnapshot CaptureSnapshot(PlayniteLibraryExporterSettings settings, string trigger, bool automatic)
        {
            var mapper = new GameExportMapper(settings.IncludeOnlyCoreFields, settings.IncludeLocalInstallPaths);
            var databaseGames = playniteApi.Database.Games.ToList();
            var filteredDatabaseGames = settings.ExcludeVisualNovelTag
                ? databaseGames.Where(game => !GameExportMapper.HasVisualNovelMetadata(game)).ToList()
                : databaseGames;
            var mappedGames = filteredDatabaseGames.Select(game => mapper.Map(game)).ToList();

            var sortedGames = mappedGames
                .OrderBy(game => string.IsNullOrWhiteSpace(game.sortingName) ? game.name : game.sortingName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(game => game.name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(game => game.playniteId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new ExportSnapshot
            {
                DatabaseGameCount = databaseGames.Count,
                ExcludedGameCount = databaseGames.Count - filteredDatabaseGames.Count,
                SteamAppIdCount = sortedGames.Count(game => game.steamAppId.HasValue),
                Trigger = trigger,
                Automatic = automatic,
                Formats = settings.GetSelectedFormats(),
                Games = sortedGames
            };
        }

        private ExportRunResult RenderAndWrite(PlayniteLibraryExporterSettings settings, ExportSnapshot snapshot)
        {
            var result = new ExportRunResult
            {
                Success = true,
                OutputDirectory = settings.OutputDirectory,
                DatabaseGameCount = snapshot.DatabaseGameCount,
                ExportedGameCount = snapshot.Games.Count,
                ExcludedGameCount = snapshot.ExcludedGameCount,
                SteamAppIdCount = snapshot.SteamAppIdCount,
                Formats = snapshot.Formats.ToList()
            };

            foreach (var format in snapshot.Formats)
            {
                try
                {
                    var content = Render(format, snapshot, settings);
                    var outputPath = fileWriter.WriteAtomic(settings.OutputDirectory, settings.OutputFileBaseName, format, content);
                    result.OutputPaths.Add(outputPath);
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.FailedFormats.Add(format.ToDisplayName());
                    result.Error = result.Error ?? ex;
                    logger.Error(ex, $"Failed writing {format.ToId()} export.");
                }
            }

            return result;
        }

        private string Render(ExportFormat format, ExportSnapshot snapshot, PlayniteLibraryExporterSettings settings)
        {
            switch (format)
            {
                case ExportFormat.Json:
                    return jsonRenderer.Render(snapshot, settings, GetPluginVersion());
                case ExportFormat.Text:
                    return textRenderer.Render(snapshot);
                case ExportFormat.Markdown:
                    return markdownRenderer.Render(snapshot);
                case ExportFormat.Csv:
                    return csvRenderer.Render(snapshot);
                default:
                    throw new NotSupportedException($"Unsupported output format: {format}");
            }
        }

        private static string GetPluginVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        }
    }
}
