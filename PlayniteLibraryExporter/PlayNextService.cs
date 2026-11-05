using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PlayniteLibraryExporter
{
    /// <summary>
    /// Play next: games marked on the website get the "Play next" tag here, a "Play next" filter preset shows them, and
    /// the ones that aren't installed are offered for install, once. Launching a game tagged this way removes the tag.
    /// Checks follow the extension's own requests to the website, never a timer (the website's docs/spec-play-next.md).
    /// </summary>
    public class PlayNextService
    {
        public const string TagName = "Play next";
        private const string InstallErrorNotificationId = "PlayniteLibraryWebPlayNextInstall";

        private readonly IPlayniteAPI api;
        private readonly Func<PlayniteLibraryExporterSettings> getSettings;
        private readonly WebLinkStore store;
        private readonly ILogger logger;
        private readonly SemaphoreSlim checkLock = new SemaphoreSlim(1, 1);

        public PlayNextService(IPlayniteAPI api, Func<PlayniteLibraryExporterSettings> getSettings, WebLinkStore store, ILogger logger)
        {
            this.api = api;
            this.getSettings = getSettings;
            this.store = store;
            this.logger = logger;
        }

        /// <summary>
        /// Fetches the website's commands, applies them, acknowledges them and offers to install newly marked games.
        /// One check at a time: one that arrives while another runs is dropped. Never throws.
        /// </summary>
        public async Task CheckAsync(string token)
        {
            if (string.IsNullOrEmpty(token) || !await checkLock.WaitAsync(0))
            {
                return;
            }

            try
            {
                List<PlayNextCommand> commands;
                try
                {
                    commands = (await new WebApiClient().GetPlayNextAsync(token))?.commands ?? new List<PlayNextCommand>();
                }
                catch (Exception ex)
                {
                    // Offline or the site is down: quiet, the next upload tries again
                    logger.Warn($"Could not fetch Play next marks: {ex.Message}");
                    return;
                }

                if (commands.Count == 0)
                {
                    return;
                }

                // The database is changed on the UI thread, where Playnite's own views read it
                var toInstall = await OnUiThread(() => Apply(commands.OrderBy(c => c.id).ToList()));

                try
                {
                    await new WebApiClient().AckPlayNextAsync(token, commands.Max(c => c.id));
                }
                catch (Exception ex)
                {
                    // The next check delivers them again, and applying them twice changes nothing
                    logger.Warn($"Could not acknowledge Play next marks: {ex.Message}");
                }

                // Offered even if the acknowledgement failed: redelivered marks find the games tagged and don't ask again
                if (toInstall.Count > 0 && getSettings().AskToInstallPlayNext)
                {
                    await OnUiThread(() => OfferInstall(toInstall));
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Play next check failed.");
            }
            finally
            {
                checkLock.Release();
            }
        }

        /// <summary>
        /// Launching a game tagged from the website takes it off the list: the tag goes. Games the user tagged by hand
        /// keep it. The game-edited and game-stopped uploads tell the website.
        /// </summary>
        public void OnGameStarted(Game game)
        {
            try
            {
                var tagged = store.State.PlayNextTaggedFromWeb;
                if (game == null || tagged == null || !tagged.Contains(game.Id))
                {
                    return;
                }

                var tag = FindTag();
                var current = api.Database.Games.Get(game.Id) ?? game;
                if (tag != null && current.TagIds != null && current.TagIds.Remove(tag.Id))
                {
                    api.Database.Games.Update(current);
                    logger.Info($"Play next: removed the tag from \"{current.Name}\", which was just started.");
                }

                // Forgotten even if the user had already removed the tag, so a tag they add later by hand stays
                store.Update(s => s.PlayNextTaggedFromWeb?.Remove(game.Id));
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Play next: couldn't update a started game.");
            }
        }

        /// <summary>Applies the commands in id order. Returns the newly tagged games that aren't installed.</summary>
        private List<Game> Apply(List<PlayNextCommand> commands)
        {
            var taggedFromWeb = new HashSet<Guid>(store.State.PlayNextTaggedFromWeb ?? new List<Guid>());
            // Games tagged from the website before this batch were already offered for install
            var offeredBefore = new HashSet<Guid>(taggedFromWeb);
            var toInstall = new List<Game>();
            var tag = FindTag();
            var createdTag = false;
            var marked = false;

            try
            {
                foreach (var command in commands)
                {
                    var game = FindGame(command);
                    if (game == null)
                    {
                        logger.Info($"Play next: skipped {command.action} of \"{command.name}\", which isn't in this PC's library.");
                        continue;
                    }

                    if (command.action == "mark")
                    {
                        marked = true;
                        if (tag == null)
                        {
                            tag = api.Database.Tags.Add(TagName);
                            createdTag = true;
                        }

                        if (game.TagIds != null && game.TagIds.Contains(tag.Id))
                        {
                            continue; // already tagged, perhaps by hand: left as it is
                        }

                        game.TagIds = game.TagIds ?? new List<Guid>();
                        game.TagIds.Add(tag.Id);
                        api.Database.Games.Update(game);
                        taggedFromWeb.Add(game.Id);
                        if (!game.IsInstalled && !offeredBefore.Contains(game.Id) && toInstall.All(g => g.Id != game.Id))
                        {
                            toInstall.Add(game);
                        }

                        logger.Info($"Play next: tagged \"{game.Name}\".");
                    }
                    else if (command.action == "unmark")
                    {
                        if (tag != null && game.TagIds != null && game.TagIds.Remove(tag.Id))
                        {
                            api.Database.Games.Update(game);
                            logger.Info($"Play next: removed the tag from \"{game.Name}\".");
                        }

                        taggedFromWeb.Remove(game.Id);
                        toInstall.RemoveAll(g => g.Id == game.Id);
                    }
                }

                if (marked)
                {
                    EnsurePreset(tag, createdTag);
                }
            }
            finally
            {
                // Saved even when a write fails partway: the games tagged so far stay known as tagged from the website, so
                // redelivered marks don't take them for tags the user added by hand
                store.Update(s => s.PlayNextTaggedFromWeb = taggedFromWeb.ToList());
            }

            return toInstall;
        }

        /// <summary>
        /// The first time a mark arrives, creates a "Play next" filter preset on the tag (shown in full screen mode's quick
        /// filters too), unless one exists. Never again after that, so a preset the user deleted stays deleted. When the
        /// tag has to be created again later, the preset made earlier is pointed at it; a "Play next" preset the user had
        /// made themselves before the first mark is left alone.
        /// </summary>
        private void EnsurePreset(Tag tag, bool createdTag)
        {
            var presets = api.Database.FilterPresets;
            var preset = presets.FirstOrDefault(p => IsPlayNext(p.Name));
            var createdBefore = store.State.PlayNextPresetCreated;
            if (preset == null && !createdBefore)
            {
                presets.Add(new FilterPreset
                {
                    Name = TagName,
                    Settings = new FilterPresetSettings { Tag = new IdItemFilterItemProperties(tag.Id) },
                    ShowInFullscreeQuickSelection = true
                });
                logger.Info("Play next: created the \"Play next\" filter preset.");
            }
            else if (preset != null && createdTag && createdBefore)
            {
                preset.Settings = preset.Settings ?? new FilterPresetSettings();
                preset.Settings.Tag = new IdItemFilterItemProperties(tag.Id);
                presets.Update(preset);
            }

            if (!createdBefore)
            {
                store.Update(s => s.PlayNextPresetCreated = true);
            }
        }

        /// <summary>One dialog for the batch: install all, or none. Each failure gets its own error notification.</summary>
        private bool OfferInstall(List<Game> games)
        {
            var install = new MessageBoxOption("Install", true, false);
            var notNow = new MessageBoxOption("Not now", false, true);
            var names = string.Join(", ", games.Select(g => g.Name));
            var choice = api.Dialogs.ShowMessage(
                $"Marked to play next on your web library: {names}. Install now?",
                WebConfig.PluginName, MessageBoxImage.Question, new List<MessageBoxOption> { install, notNow });
            if (choice?.Title != install.Title)
            {
                return false;
            }

            foreach (var game in games)
            {
                try
                {
                    api.InstallGame(game.Id);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Play next: couldn't start installing \"{game.Name}\".");
                    api.Notifications.Add(new NotificationMessage(
                        $"{InstallErrorNotificationId}-{game.Id}", $"Couldn't start installing {game.Name}: {ex.Message}", NotificationType.Error));
                }
            }

            return true;
        }

        /// <summary>By this PC's Playnite id, else by store and the store's game id (the id differs between PCs).</summary>
        private Game FindGame(PlayNextCommand command)
        {
            if (Guid.TryParse(command.playniteId, out var id))
            {
                var game = api.Database.Games.Get(id);
                if (game != null)
                {
                    return game;
                }
            }

            if (!Guid.TryParse(command.pluginId, out var pluginId) || string.IsNullOrEmpty(command.providerGameId))
            {
                return null;
            }

            return api.Database.Games.FirstOrDefault(g => g.PluginId == pluginId && g.GameId == command.providerGameId);
        }

        private Tag FindTag()
        {
            return api.Database.Tags.FirstOrDefault(t => IsPlayNext(t.Name));
        }

        private static bool IsPlayNext(string name)
        {
            return string.Equals(name?.Trim(), TagName, StringComparison.OrdinalIgnoreCase);
        }

        private static Task<T> OnUiThread<T>(Func<T> action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                return dispatcher.InvokeAsync(action).Task;
            }

            return Task.FromResult(action());
        }
    }
}
