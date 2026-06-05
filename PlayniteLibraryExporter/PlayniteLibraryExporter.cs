using Playnite.SDK;
using Playnite.SDK.Events;
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
        private const string MenuRoot = "Playnite Library Exporter";
        private const int AutomaticExportDebounceMilliseconds = 3000;
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly PlayniteLibraryExporterSettingsViewModel settingsViewModel;
        private readonly ExportService exportService;
        private CancellationTokenSource automaticExportDebounce;

        public override Guid Id { get; } = Guid.Parse("7d089a0e-b862-44df-bca8-df3dc13165ee");

        public IPlayniteAPI Api => PlayniteApi;

        public PlayniteLibraryExporter(IPlayniteAPI api) : base(api)
        {
            settingsViewModel = new PlayniteLibraryExporterSettingsViewModel(this);
            exportService = new ExportService(api, () => settingsViewModel.Settings, logger);
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };

            logger.Info("Playnite Library Exporter loaded.");
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            logger.Info("Playnite Library Exporter application-started hook.");
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

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
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
            return new List<MainMenuItem>
            {
                MenuItem("Export library now", $"{MenuRoot}", menuArgs => RunManualExport()),
                MenuItem(FormatToggleLabel(settings.OutputJson, "JSON"), $"{MenuRoot}|Output formats", _ => ToggleFormat(ExportFormat.Json)),
                MenuItem(FormatToggleLabel(settings.OutputText, "Plain text (.txt)"), $"{MenuRoot}|Output formats", _ => ToggleFormat(ExportFormat.Text)),
                MenuItem(FormatToggleLabel(settings.OutputMarkdown, "Markdown (.md)"), $"{MenuRoot}|Output formats", _ => ToggleFormat(ExportFormat.Markdown)),
                MenuItem(FormatToggleLabel(settings.OutputCsv, "Spreadsheet CSV (.csv)"), $"{MenuRoot}|Output formats", _ => ToggleFormat(ExportFormat.Csv)),
                MenuItem(FormatToggleLabel(settings.ExcludeVisualNovelTag, "Exclude games with tag/genre \"visual novel\""), $"{MenuRoot}", _ => ToggleVisualNovelFilter()),
                MenuItem("Open export folder", $"{MenuRoot}", _ => OpenExportFolder()),
                MenuItem("Open settings", $"{MenuRoot}", _ => PlayniteApi.MainView.OpenPluginSettings(Id))
            };
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
                PlayniteApi.Dialogs.ShowErrorMessage("At least one output format must remain selected.", "Playnite Library Exporter");
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
                    PlayniteApi.Notifications.Add("PlayniteLibraryExporterSuccess", FormatSuccessSummary(result), NotificationType.Info);
                }
                else if (!automatic && settings.ShowNotificationAfterManualExport)
                {
                    PlayniteApi.Dialogs.ShowMessage(FormatSuccessMessage(result), "Playnite Library Exporter");
                }
            }
            else
            {
                var message = FormatFailureMessage(result);
                if (automatic)
                {
                    PlayniteApi.Notifications.Add("PlayniteLibraryExporterFailure", message, NotificationType.Error);
                }
                else
                {
                    PlayniteApi.Dialogs.ShowErrorMessage(message, "Playnite Library Exporter");
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
