using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace PlayniteLibraryExporter
{
    public class PlayniteLibraryExporterSettings : ObservableObject
    {
        private string deviceName = Environment.MachineName;
        private bool autoUpload = true;
        private bool includeHiddenGames;
        private bool excludeVisualNovelTag;

        /// <summary>Name shown on the website's approval screen and in the list of linked PCs.</summary>
        public string DeviceName
        {
            get => deviceName;
            set => SetValue(ref deviceName, value);
        }

        /// <summary>Upload on startup, after library updates, after playing and after edits.</summary>
        public bool AutoUpload
        {
            get => autoUpload;
            set => SetValue(ref autoUpload, value);
        }

        public bool IncludeHiddenGames
        {
            get => includeHiddenGames;
            set => SetValue(ref includeHiddenGames, value);
        }

        public bool ExcludeVisualNovelTag
        {
            get => excludeVisualNovelTag;
            set => SetValue(ref excludeVisualNovelTag, value);
        }

        public void Normalize()
        {
            DeviceName = string.IsNullOrWhiteSpace(DeviceName) ? Environment.MachineName : DeviceName.Trim();
        }
    }

    public class PlayniteLibraryExporterSettingsViewModel : ObservableObject, ISettings
    {
        private readonly PlayniteLibraryExporter plugin;
        private PlayniteLibraryExporterSettings editingClone;
        private PlayniteLibraryExporterSettings settings;
        private WebSyncService web;

        public PlayniteLibraryExporterSettings Settings
        {
            get => settings;
            set
            {
                settings = value;
                OnPropertyChanged();
            }
        }

        public ICommand LinkCommand { get; }
        public ICommand CancelLinkCommand { get; }
        public ICommand UnlinkCommand { get; }
        public ICommand UploadNowCommand { get; }
        public ICommand OpenLibraryCommand { get; }
        public ICommand CopyLibraryLinkCommand { get; }
        public ICommand OpenAccountCommand { get; }

        public PlayniteLibraryExporterSettingsViewModel(PlayniteLibraryExporter plugin)
        {
            this.plugin = plugin;
            LinkCommand = new RelayCommand(() => plugin.StartWebLink());
            CancelLinkCommand = new RelayCommand(() => web?.CancelLink());
            UnlinkCommand = new RelayCommand(() => plugin.UnlinkWeb());
            UploadNowCommand = new RelayCommand(() => plugin.UploadToWebNow());
            OpenLibraryCommand = new RelayCommand(() => web?.OpenLibrary());
            CopyLibraryLinkCommand = new RelayCommand(CopyLibraryLink);
            OpenAccountCommand = new RelayCommand(() => web?.OpenAccountPage());

            Settings = plugin.LoadPluginSettings<PlayniteLibraryExporterSettings>() ??
                       new PlayniteLibraryExporterSettings();
            Settings.Normalize();
        }

        public void AttachWeb(WebSyncService service)
        {
            web = service;
            web.StatusChanged += (_, __) => RaiseWebStatusChanged();
        }

        // ---------- Web library status (bound by the settings view) ----------

        public bool IsWebLinked => web?.Phase == WebLinkPhase.Linked;
        public bool IsWebLinking => web?.Phase == WebLinkPhase.Linking;
        public bool IsWebNotLinked => web == null || web.Phase == WebLinkPhase.NotLinked;
        public string WebLinkCode => web?.LinkCode;
        public string WebLibraryUrl => web?.State.LibraryUrl;
        public string WebError => web?.LastError;
        public bool HasWebError => !string.IsNullOrEmpty(WebError);

        public string WebStatusText
        {
            get
            {
                if (web == null)
                {
                    return string.Empty;
                }

                switch (web.Phase)
                {
                    case WebLinkPhase.Linking:
                        return $"Waiting for approval. In the browser window that opened, sign in and confirm the code {web.LinkCode}.";
                    case WebLinkPhase.Linked:
                        var state = web.State;
                        var last = state.LastUploadUtc.HasValue
                            ? "Last upload: " + state.LastUploadUtc.Value.ToLocalTime().ToString("g")
                            : "Not uploaded yet.";
                        return $"Linked as {state.Handle}. {last}";
                    default:
                        return "Not linked. Link this PC to publish your library as a web page that stays up to date.";
                }
            }
        }

        private void RaiseWebStatusChanged()
        {
            void Raise()
            {
                foreach (var name in new[]
                {
                    nameof(IsWebLinked), nameof(IsWebLinking), nameof(IsWebNotLinked), nameof(WebLinkCode),
                    nameof(WebLibraryUrl), nameof(WebError), nameof(HasWebError), nameof(WebStatusText)
                })
                {
                    OnPropertyChanged(name);
                }
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke((Action)Raise);
            }
            else
            {
                Raise();
            }
        }

        private void CopyLibraryLink()
        {
            var url = WebLibraryUrl;
            if (!string.IsNullOrEmpty(url))
            {
                System.Windows.Clipboard.SetText(url);
            }
        }

        // ---------- ISettings ----------

        public void BeginEdit()
        {
            editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            Settings = editingClone;
        }

        public void EndEdit()
        {
            Save();
            // Hidden games or the filter may have changed what gets uploaded
            web?.ScheduleUpload("settings-changed", WebConfig.LibraryUpdatedDelayMs);
        }

        public void Save()
        {
            Settings.Normalize();
            plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            return true;
        }
    }
}
