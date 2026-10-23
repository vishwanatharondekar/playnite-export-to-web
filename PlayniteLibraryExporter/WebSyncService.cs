using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PlayniteLibraryExporter
{
    public enum WebLinkPhase
    {
        NotLinked,
        Linking,
        Linked
    }

    /// <summary>Links this PC to a web library account and keeps the library uploaded.</summary>
    public class WebSyncService
    {
        private const string ErrorNotificationId = "PlayniteLibraryWebUploadError";
        private const string LinkedNotificationId = "PlayniteLibraryWebLinked";

        private readonly IPlayniteAPI api;
        private readonly Func<PlayniteLibraryExporterSettings> getSettings;
        private readonly WebLinkStore store;
        private readonly ILogger logger;
        private readonly SemaphoreSlim uploadLock = new SemaphoreSlim(1, 1);
        private readonly object scheduleSync = new object();
        private CancellationTokenSource pendingUpload;
        private bool pendingForce;
        private CancellationTokenSource linking;
        private DateTime notBeforeUtc = DateTime.MinValue;

        /// <summary>Raised (on any thread) whenever the link or upload status changes.</summary>
        public event EventHandler StatusChanged;

        public WebLinkPhase Phase { get; private set; }
        /// <summary>Code to confirm in the browser while linking, e.g. "WDJB-MJHT".</summary>
        public string LinkCode { get; private set; }
        public string LastError { get; private set; }

        public WebSyncService(IPlayniteAPI api, Func<PlayniteLibraryExporterSettings> getSettings, WebLinkStore store, ILogger logger)
        {
            this.api = api;
            this.getSettings = getSettings;
            this.store = store;
            this.logger = logger;
            Phase = CurrentToken() == null ? WebLinkPhase.NotLinked : WebLinkPhase.Linked;
        }

        public WebLinkState State => store.State;
        public bool IsLinked => Phase == WebLinkPhase.Linked;

        private string CurrentToken() => store.GetToken();

        // ---------- Linking ----------

        /// <summary>
        /// Starts the device link: opens the browser on the approval page and polls until the user
        /// approves, denies, or the code expires. Returns the code to show to the user.
        /// </summary>
        public async Task<string> StartLinkAsync()
        {
            CancelLink();
            var settings = getSettings();
            var client = new WebApiClient();
            var deviceName = string.IsNullOrWhiteSpace(settings.DeviceName) ? Environment.MachineName : settings.DeviceName.Trim();

            LastError = null;
            var start = await client.StartLinkAsync(deviceName);
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(60, start.expiresIn)));
            linking = cts;
            LinkCode = start.userCode;
            SetPhase(WebLinkPhase.Linking);
            OpenUrl(start.verifyUrl);

            _ = Task.Run(() => PollLinkAsync(client, start, cts.Token));
            return start.userCode;
        }

        public void CancelLink()
        {
            linking?.Cancel();
            linking = null;
            if (Phase == WebLinkPhase.Linking)
            {
                LinkCode = null;
                SetPhase(CurrentToken() == null ? WebLinkPhase.NotLinked : WebLinkPhase.Linked);
            }
        }

        private async Task PollLinkAsync(WebApiClient client, DeviceStartResponse start, CancellationToken token)
        {
            var interval = TimeSpan.FromSeconds(Math.Max(2, start.interval));
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(interval, token);
                    var result = await client.PollLinkAsync(start.deviceCode);
                    if (result == null)
                    {
                        continue;
                    }

                    store.SaveLink(result.deviceToken, result.handle, result.url);
                    LinkCode = null;
                    SetPhase(WebLinkPhase.Linked);
                    logger.Info($"Linked to web library {result.url}");
                    api.Notifications.Add(LinkedNotificationId, $"Linked to your web library: {result.url}", NotificationType.Info);
                    await UploadNowAsync("linked", true);
                    return;
                }
            }
            catch (TaskCanceledException)
            {
                // Cancelled or expired; handled below
            }
            catch (WebApiException ex)
            {
                // access_denied or expired_token
                LastError = ex.ErrorCode == "access_denied" ? "The link request was denied." : ex.Message;
                logger.Warn($"Web library link failed: {ex.ErrorCode} {ex.Message}");
            }
            catch (Exception ex)
            {
                LastError = "Linking failed: " + ex.Message;
                logger.Error(ex, "Web library link failed.");
            }

            if (Phase == WebLinkPhase.Linking && linking?.Token == token)
            {
                if (LastError == null)
                {
                    LastError = "The code expired before it was approved. Try again.";
                }

                LinkCode = null;
                SetPhase(CurrentToken() == null ? WebLinkPhase.NotLinked : WebLinkPhase.Linked);
            }
        }

        public async Task UnlinkAsync()
        {
            CancelUpload();
            var token = CurrentToken();
            if (token != null)
            {
                try
                {
                    await new WebApiClient().UnlinkAsync(token);
                }
                catch (Exception ex)
                {
                    // Unlink locally anyway; the token can also be revoked from the website
                    logger.Warn($"Could not revoke the device token on the server: {ex.Message}");
                }
            }

            store.Clear();
            LastError = null;
            SetPhase(WebLinkPhase.NotLinked);
        }

        // ---------- Uploading ----------

        /// <summary>Schedules an upload after a quiet period; a newer request replaces a pending one.</summary>
        public void ScheduleUpload(string trigger, int delayMs, bool force = false)
        {
            if (!IsLinked || !getSettings().AutoUpload)
            {
                return;
            }

            CancellationTokenSource cts;
            lock (scheduleSync)
            {
                // A replaced forced upload (e.g. on startup) stays forced
                if (pendingUpload != null && !pendingUpload.IsCancellationRequested && pendingForce)
                {
                    force = true;
                }

                pendingUpload?.Cancel();
                pendingUpload = cts = new CancellationTokenSource();
                pendingForce = force;
            }

            Task.Run(async () =>
            {
                try
                {
                    var wait = TimeSpan.FromMilliseconds(delayMs);
                    var rateLimited = notBeforeUtc - DateTime.UtcNow;
                    await Task.Delay(rateLimited > wait ? rateLimited : wait, cts.Token);
                    lock (scheduleSync)
                    {
                        if (pendingUpload == cts)
                        {
                            pendingUpload = null;
                            pendingForce = false;
                        }
                    }

                    await UploadNowAsync(trigger, force);
                }
                catch (TaskCanceledException)
                {
                }
            });
        }

        private void CancelUpload()
        {
            lock (scheduleSync)
            {
                pendingUpload?.Cancel();
                pendingUpload = null;
            }
        }

        /// <summary>
        /// Uploads the library now. Unless forced, skips the request when nothing changed since the
        /// last successful upload (the server would treat it as a no-op anyway).
        /// </summary>
        public async Task<bool> UploadNowAsync(string trigger, bool force)
        {
            var token = CurrentToken();
            if (token == null)
            {
                return false;
            }

            await uploadLock.WaitAsync();
            try
            {
                var payload = await CaptureAsync(trigger);
                if (!force && payload.Hash == store.State.LastUploadedHash)
                {
                    logger.Debug($"Web library upload skipped, unchanged. Trigger={trigger}");
                    return true;
                }

                var client = new WebApiClient();
                var response = await client.UploadExportAsync(token, payload.Json);
                // Picks up handle renames on the website and a new domain after the server moves
                var me = force ? await TryGetMeAsync(client, token) : null;
                store.Update(s =>
                {
                    s.LastUploadUtc = DateTime.UtcNow;
                    s.LastUploadedHash = payload.Hash;
                    if (me != null)
                    {
                        s.Handle = me.handle;
                        s.LibraryUrl = me.url;
                    }
                });
                LastError = null;
                api.Notifications.Remove(ErrorNotificationId);
                logger.Info($"Web library upload done. Trigger={trigger}, Games={payload.GameCount}, Updated={response?.updated}");
                StatusChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch (WebApiException ex) when (ex.IsUnauthorized)
            {
                logger.Warn("Web library token was rejected; this PC must be linked again.");
                store.Clear();
                SetPhase(WebLinkPhase.NotLinked);
                ReportError("This PC was unlinked from your web library. Open the extension settings and link it again.");
                return false;
            }
            catch (WebApiException ex) when (ex.IsRateLimited)
            {
                var wait = ex.RetryAfter ?? TimeSpan.FromMinutes(10);
                notBeforeUtc = DateTime.UtcNow + wait;
                logger.Warn($"Web library upload rate-limited; retrying in {wait}.");
                ScheduleUpload(trigger, (int)wait.TotalMilliseconds);
                return false;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Web library upload failed. Trigger={trigger}");
                ReportError("Uploading your library failed: " + ex.Message);
                return false;
            }
            finally
            {
                uploadLock.Release();
            }
        }

        private async Task<MeResponse> TryGetMeAsync(WebApiClient client, string token)
        {
            try
            {
                return await client.GetMeAsync(token);
            }
            catch (Exception ex)
            {
                logger.Warn($"Could not refresh the web library link: {ex.Message}");
                return null;
            }
        }

        private void ReportError(string message)
        {
            LastError = message;
            // Same id replaces the previous error instead of piling up notifications
            api.Notifications.Add(ErrorNotificationId, message, NotificationType.Error);
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }

        private class UploadPayload
        {
            public string Json { get; set; }
            public string Hash { get; set; }
            public int GameCount { get; set; }
        }

        /// <summary>Snapshot of the library for upload. The database is read on the UI thread, like the file export.</summary>
        private Task<UploadPayload> CaptureAsync(string trigger)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                return dispatcher.InvokeAsync(() => Capture(trigger)).Task;
            }

            return Task.FromResult(Capture(trigger));
        }

        private UploadPayload Capture(string trigger)
        {
            var settings = getSettings();
            var databaseGames = api.Database.Games.ToList();
            var included = databaseGames
                .Where(game => settings.IncludeHiddenGames || !game.Hidden)
                .Where(game => !settings.ExcludeVisualNovelTag || !GameExportMapper.HasVisualNovelMetadata(game))
                .ToList();
            var games = included
                .Select(game => GameExportMapper.Map(game))
                .OrderBy(game => string.IsNullOrWhiteSpace(game.sortingName) ? game.name : game.sortingName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(game => game.name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(game => game.playniteId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var envelope = new ExportEnvelopeDto
            {
                schemaVersion = 1,
                generatedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
                generator = new GeneratorDto
                {
                    name = WebConfig.PluginName,
                    version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0"
                },
                playnite = new PlayniteDto { databaseGameCount = databaseGames.Count },
                export = new ExportSummaryDto
                {
                    totalGames = games.Count,
                    databaseGameCount = databaseGames.Count,
                    excludedGameCount = databaseGames.Count - included.Count,
                    steamAppIdCount = games.Count(game => game.steamAppId.HasValue),
                    automatic = trigger != "manual",
                    trigger = trigger
                },
                games = games
            };

            return new UploadPayload
            {
                Json = Serialization.ToJson(envelope, false),
                Hash = Sha256(Serialization.ToJson(games, false)),
                GameCount = games.Count
            };
        }

        private static string Sha256(string text)
        {
            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", string.Empty);
            }
        }

        // ---------- Helpers ----------

        public void OpenLibrary()
        {
            var url = store.State.LibraryUrl;
            if (!string.IsNullOrEmpty(url))
            {
                OpenUrl(url);
            }
        }

        public void OpenAccountPage()
        {
            OpenUrl(WebConfig.ServerUrl.TrimEnd('/') + "/account");
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Could not open {url}");
            }
        }

        private void SetPhase(WebLinkPhase phase)
        {
            Phase = phase;
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
