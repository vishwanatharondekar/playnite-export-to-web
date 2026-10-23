using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace PlayniteLibraryExporter
{
    public class PlayniteLibraryExporter : GenericPlugin
    {
        private const string MenuRoot = WebConfig.PluginName;
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly PlayniteLibraryExporterSettingsViewModel settingsViewModel;
        private readonly WebSyncService webSync;

        public override Guid Id { get; } = Guid.Parse("6470f7f6-0f8e-4f44-a3f9-86c1cc82028f");

        public PlayniteLibraryExporter(IPlayniteAPI api) : base(api)
        {
            settingsViewModel = new PlayniteLibraryExporterSettingsViewModel(this);
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
            PlayniteApi.Database.Games.ItemUpdated += OnGamesUpdated;
            PlayniteApi.Database.Games.ItemCollectionChanged += OnGamesAddedOrRemoved;
            // Forced: the server skips unchanged libraries, and this recovers from anything missed while closed
            webSync.ScheduleUpload("startup", WebConfig.LibraryUpdatedDelayMs, force: true);
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            PlayniteApi.Database.Games.ItemUpdated -= OnGamesUpdated;
            PlayniteApi.Database.Games.ItemCollectionChanged -= OnGamesAddedOrRemoved;
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            webSync.ScheduleUpload("library-updated", WebConfig.LibraryUpdatedDelayMs);
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

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            var items = webSync.IsLinked
                ? new List<MainMenuItem>
                {
                    MenuItem("Open my web library", _ => webSync.OpenLibrary()),
                    MenuItem("Upload to web library now", _ => UploadToWebNow()),
                    MenuItem("Unlink this PC from the web library", _ => UnlinkWeb())
                }
                : new List<MainMenuItem>
                {
                    MenuItem("Link web library account...", _ => StartWebLink())
                };

            var exclude = settingsViewModel.Settings.ExcludeVisualNovelTag;
            items.Add(MenuItem($"[{(exclude ? "x" : " ")}] Exclude games with tag/genre \"visual novel\"", _ => ToggleVisualNovelFilter()));
            items.Add(MenuItem("Open settings", _ => PlayniteApi.MainView.OpenPluginSettings(Id)));
            return items;
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settingsViewModel;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new PlayniteLibraryExporterSettingsView();
        }

        private static MainMenuItem MenuItem(string description, Action<MainMenuItemActionArgs> action)
        {
            return new MainMenuItem
            {
                Description = description,
                MenuSection = MenuRoot,
                Action = action
            };
        }

        private void ToggleVisualNovelFilter()
        {
            settingsViewModel.Settings.ExcludeVisualNovelTag = !settingsViewModel.Settings.ExcludeVisualNovelTag;
            settingsViewModel.Save();
            webSync.ScheduleUpload("filter-changed", WebConfig.LibraryUpdatedDelayMs);
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
    }
}
