using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PlayniteLibraryExporter
{
    public class PlayniteLibraryExporter : GenericPlugin
    {
        private const string MenuRoot = WebConfig.PluginName;
        private const int AutomaticExportDebounceMilliseconds = 3000;
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly PlayniteLibraryExporterSettingsViewModel settingsViewModel;
        private readonly ExportService exportService;
        private readonly WebSyncService webSync;
        private CancellationTokenSource automaticExportDebounce;

        public override Guid Id { get; } = Guid.Parse("6470f7f6-0f8e-4f44-a3f9-86c1cc82028f");

        public IPlayniteAPI Api => PlayniteApi;

        public PlayniteLibraryExporter(IPlayniteAPI api) : base(api)
        {
            settingsViewModel = new PlayniteLibraryExporterSettingsViewModel(this);
            exportService = new ExportService(api, () => settingsViewModel.Settings, logger);
            webSync = new WebSyncService(api, () => settingsViewModel.Settings, new WebLinkStore(GetPluginUserDataPath()), logger);
            settingsViewModel.AttachWeb(webSync);
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };

            logger.Info($"{MenuRoot} loaded.");
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            logger.Info($"{MenuRoot} application-started hook.");
            PlayniteApi.Database.Games.ItemUpdated += OnGamesUpdated;
            PlayniteApi.Database.Games.ItemCollectionChanged += OnGamesAddedOrRemoved;
            // Forced: the server skips unchanged libraries, and this recovers from anything missed while closed
            webSync.ScheduleUpload("startup", WebConfig.LibraryUpdatedDelayMs, force: true);

            var settings = settingsViewModel.Settings;
            if (settings.ExportOnEveryPlayniteStartup)
            {
                _ = RunExportAndNotifyAsync("startup", true);
                return;
            }

            if (settings.ExportOnceOnStartupIfFileIsMissing && !exportService.AnyConfiguredOutputExists())
            {
                _ = RunExportAndNotifyAsync("startup-missing-file", true);
            }
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            PlayniteApi.Database.Games.ItemUpdated -= OnGamesUpdated;
            PlayniteApi.Database.Games.ItemCollectionChanged -= OnGamesAddedOrRemoved;
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            // Playtime and last played changed
            webSync.ScheduleUpload("game-stopped", WebConfig.GameStoppedDelayMs);
        }

        private void OnGamesUpdated(object sender, ItemUpdatedEventArgs<Game> args)
        {
            // Edits such as tags, favourites or hiding; bursts (e.g. metadata downloads) upload once
            webSync.ScheduleUpload("game-edited", WebConfig.GameEditedDelayMs);
        }

        private void OnGamesAddedOrRemoved(object sender, ItemCollectionChangedEventArgs<Game> args)
        {
            webSync.ScheduleUpload("games-added-removed", WebConfig.GameEditedDelayMs);
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            webSync.ScheduleUpload("library-updated", WebConfig.LibraryUpdatedDelayMs);

            if (!settingsViewModel.Settings.EnableAutomaticExportAfterLibraryUpdate)
            {
                return;
            }

            logger.Info("Playnite library update detected; scheduling export.");
            ScheduleAutomaticExport();
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            var settings = settingsViewModel.Settings;
            var webItems = webSync.IsLinked
                ? new List<MainMenuItem>
                {
                    MenuItem("Open my web library", $"{MenuRoot}", _ => webSync.OpenLibrary()),
                    MenuItem("Upload to web library now", $"{MenuRoot}", _ => UploadToWebNow()),
                    MenuItem("Unlink this PC from the web library", $"{MenuRoot}", _ => UnlinkWeb())
                }
                : new List<MainMenuItem>
                {
                    MenuItem("Link web library account...", $"{MenuRoot}", _ => StartWebLink())
                };

            return webItems.Concat(new List<MainMenuItem>
            {
                MenuItem("Export library to file now", $"{MenuRoot}", menuArgs => RunManualExport()),
                MenuItem(FormatToggleLabel(settings.OutputJson, "JSON"), $"{MenuRoot}|Output formats", _ => ToggleFormat(ExportFormat.Json)),
                MenuItem(FormatToggleLabel(settings.OutputText, "Plain text (.txt)"), $"{MenuRoot}|Output formats", _ => ToggleFormat(ExportFormat.Text)),
                MenuItem(FormatToggleLabel(settings.OutputMarkdown, "Markdown (.md)"), $"{MenuRoot}|Output formats", _ => ToggleFormat(ExportFormat.Markdown)),
                MenuItem(FormatToggleLabel(settings.OutputCsv, "Spreadsheet CSV (.csv)"), $"{MenuRoot}|Output formats", _ => ToggleFormat(ExportFormat.Csv)),
                MenuItem(FormatToggleLabel(settings.ExcludeVisualNovelTag, "Exclude games with tag/genre \"visual novel\""), $"{MenuRoot}", _ => ToggleVisualNovelFilter()),
                MenuItem("Open export folder", $"{MenuRoot}", _ => OpenExportFolder()),
                MenuItem("Open settings", $"{MenuRoot}", _ => PlayniteApi.MainView.OpenPluginSettings(Id))
            });
        }

        // ---------- Web library ----------

        public async void StartWebLink()
        {
            try
            {
                var code = await webSync.StartLinkAsync();
                PlayniteApi.Dialogs.ShowMessage(
                    "A browser window opened on your web library." + Environment.NewLine + Environment.NewLine +
                    "Sign in, then check that it shows this code and click Link:" + Environment.NewLine + Environment.NewLine +
                    code + Environment.NewLine + Environment.NewLine +
                    "This PC links automatically once you approve, and your library uploads right after.",
                    MenuRoot);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Could not start web library linking.");
                PlayniteApi.Dialogs.ShowErrorMessage("Could not start linking: " + ex.Message, MenuRoot);
            }
        }

        public async void UnlinkWeb()
        {
            var answer = PlayniteApi.Dialogs.ShowMessage(
                "Stop uploading from this PC? Your web library stays online with its last upload; delete it from the website if you want it gone.",
                MenuRoot, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Yes)
            {
                await webSync.UnlinkAsync();
            }
        }

        public async void UploadToWebNow()
        {
            if (!webSync.IsLinked)
            {
                StartWebLink();
                return;
            }

            if (await webSync.UploadNowAsync("manual", true))
            {
                PlayniteApi.Dialogs.ShowMessage($"Your library was uploaded.{Environment.NewLine}{webSync.State.LibraryUrl}", MenuRoot);
            }
            else if (!string.IsNullOrEmpty(webSync.LastError))
            {
                PlayniteApi.Dialogs.ShowErrorMessage(webSync.LastError, MenuRoot);
            }
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settingsViewModel;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new PlayniteLibraryExporterSettingsView();
        }

        private static MainMenuItem MenuItem(string description, string section, Action<MainMenuItemActionArgs> action)
        {
            return new MainMenuItem
            {
                Description = description,
                MenuSection = section,
                Action = action
            };
        }

        private static string FormatToggleLabel(bool enabled, string label)
        {
            return $"[{(enabled ? "x" : " ")}] {label}";
        }

        private void RunManualExport()
        {
            var _ = RunExportAndNotifyAsync("manual", false);
        }

        private void ToggleFormat(ExportFormat format)
        {
            var settings = settingsViewModel.Settings;
            switch (format)
            {
                case ExportFormat.Json:
                    settings.OutputJson = !settings.OutputJson;
                    break;
                case ExportFormat.Text:
                    settings.OutputText = !settings.OutputText;
                    break;
                case ExportFormat.Markdown:
                    settings.OutputMarkdown = !settings.OutputMarkdown;
                    break;
                case ExportFormat.Csv:
                    settings.OutputCsv = !settings.OutputCsv;
                    break;
            }

            if (!settings.GetSelectedFormats().Any())
            {
                SetFormat(format, true);
                PlayniteApi.Dialogs.ShowErrorMessage("At least one output format must remain selected.", MenuRoot);
                return;
            }

            settingsViewModel.Save();
        }

        private void SetFormat(ExportFormat format, bool enabled)
        {
            switch (format)
            {
                case ExportFormat.Json:
                    settingsViewModel.Settings.OutputJson = enabled;
                    break;
                case ExportFormat.Text:
                    settingsViewModel.Settings.OutputText = enabled;
                    break;
                case ExportFormat.Markdown:
                    settingsViewModel.Settings.OutputMarkdown = enabled;
                    break;
                case ExportFormat.Csv:
                    settingsViewModel.Settings.OutputCsv = enabled;
                    break;
            }
        }

        private void ToggleVisualNovelFilter()
        {
            settingsViewModel.Settings.ExcludeVisualNovelTag = !settingsViewModel.Settings.ExcludeVisualNovelTag;
            settingsViewModel.Save();
        }

        private void OpenExportFolder()
        {
            var path = Environment.ExpandEnvironmentVariables(settingsViewModel.Settings.OutputDirectory ?? string.Empty);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            Process.Start(path);
        }

        private void ScheduleAutomaticExport()
        {
            automaticExportDebounce?.Cancel();
            automaticExportDebounce = new CancellationTokenSource();
            var token = automaticExportDebounce.Token;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(AutomaticExportDebounceMilliseconds, token);
                    if (!token.IsCancellationRequested)
                    {
                        await RunExportAndNotifyAsync("library-updated", true);
                    }
                }
                catch (TaskCanceledException)
                {
                }
            }, token);
        }

        private async Task RunExportAndNotifyAsync(string trigger, bool automatic)
        {
            var result = await exportService.ExportAsync(trigger, automatic);
            var settings = settingsViewModel.Settings;

            if (result.Success)
            {
                if (automatic && settings.ShowNotificationAfterAutomaticExport)
                {
                    PlayniteApi.Notifications.Add("PlayniteLibraryWebExportSuccess", FormatSuccessSummary(result), NotificationType.Info);
                }
                else if (!automatic && settings.ShowNotificationAfterManualExport)
                {
                    PlayniteApi.Dialogs.ShowMessage(FormatSuccessMessage(result), MenuRoot);
                }
            }
            else
            {
                var message = FormatFailureMessage(result);
                if (automatic)
                {
                    PlayniteApi.Notifications.Add("PlayniteLibraryWebExportFailure", message, NotificationType.Error);
                }
                else
                {
                    PlayniteApi.Dialogs.ShowErrorMessage(message, MenuRoot);
                }
            }
        }

        private static string FormatSuccessSummary(ExportRunResult result)
        {
            return $"Exported {result.ExportedGameCount} games to {result.OutputPaths.Count} file(s).";
        }

        private static string FormatSuccessMessage(ExportRunResult result)
        {
            return "Playnite library exported successfully." +
                   Environment.NewLine + Environment.NewLine +
                   $"Formats: {string.Join(", ", result.Formats.Select(format => format.ToDisplayName()))}" +
                   Environment.NewLine +
                   $"Games exported: {result.ExportedGameCount}" +
                   Environment.NewLine +
                   $"Games excluded by filters: {result.ExcludedGameCount}" +
                   Environment.NewLine +
                   $"Steam AppIDs found: {result.SteamAppIdCount}" +
                   Environment.NewLine +
                   "Outputs:" +
                   Environment.NewLine +
                   string.Join(Environment.NewLine, result.OutputPaths);
        }

        private static string FormatFailureMessage(ExportRunResult result)
        {
            var failedFormats = result.FailedFormats.Any()
                ? Environment.NewLine + "Failed formats: " + string.Join(", ", result.FailedFormats)
                : string.Empty;

            return "Playnite library export failed." +
                   Environment.NewLine + Environment.NewLine +
                   "Reason: " + (result.Error?.Message ?? "Unknown error.") +
                   failedFormats +
                   Environment.NewLine +
                   "Please check the plugin settings and Playnite logs.";
        }
    }
}
