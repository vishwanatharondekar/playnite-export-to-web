using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace PlayniteLibraryExporter
{
    public class PlayniteLibraryExporterSettings : ObservableObject
    {
        private bool enableAutomaticExportAfterLibraryUpdate = true;
        private bool exportOnceOnStartupIfFileIsMissing = true;
        private bool exportOnEveryPlayniteStartup;
        private bool outputJson = true;
        private bool outputText;
        private bool outputMarkdown;
        private bool outputCsv;
        private bool excludeVisualNovelTag;
        private string outputDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "PlayniteLibraryExport");
        private string outputFileBaseName = "playnite-library";
        private bool prettyPrintJson = true;
        private bool includeExportMetadataEnvelope = true;
        private bool includeOnlyCoreFields;
        private bool showNotificationAfterAutomaticExport;
        private bool showNotificationAfterManualExport = true;
        private bool includeLocalInstallPaths = true;
        private string serverUrl = WebConfig.DefaultServerUrl;
        private string deviceName = Environment.MachineName;
        private bool autoUpload = true;
        private bool includeHiddenGames;

        public bool EnableAutomaticExportAfterLibraryUpdate
        {
            get => enableAutomaticExportAfterLibraryUpdate;
            set => SetValue(ref enableAutomaticExportAfterLibraryUpdate, value);
        }

        public bool ExportOnceOnStartupIfFileIsMissing
        {
            get => exportOnceOnStartupIfFileIsMissing;
            set => SetValue(ref exportOnceOnStartupIfFileIsMissing, value);
        }

        public bool ExportOnEveryPlayniteStartup
        {
            get => exportOnEveryPlayniteStartup;
            set => SetValue(ref exportOnEveryPlayniteStartup, value);
        }

        public bool OutputJson
        {
            get => outputJson;
            set => SetValue(ref outputJson, value);
        }

        public bool OutputText
        {
            get => outputText;
            set => SetValue(ref outputText, value);
        }

        public bool OutputMarkdown
        {
            get => outputMarkdown;
            set => SetValue(ref outputMarkdown, value);
        }

        public bool OutputCsv
        {
            get => outputCsv;
            set => SetValue(ref outputCsv, value);
        }

        public bool ExcludeVisualNovelTag
        {
            get => excludeVisualNovelTag;
            set => SetValue(ref excludeVisualNovelTag, value);
        }

        public string OutputDirectory
        {
            get => outputDirectory;
            set => SetValue(ref outputDirectory, value);
        }

        public string OutputFileBaseName
        {
            get => outputFileBaseName;
            set => SetValue(ref outputFileBaseName, value);
        }

        public bool PrettyPrintJson
        {
            get => prettyPrintJson;
            set => SetValue(ref prettyPrintJson, value);
        }

        public bool IncludeExportMetadataEnvelope
        {
            get => includeExportMetadataEnvelope;
            set => SetValue(ref includeExportMetadataEnvelope, value);
        }

        public bool IncludeOnlyCoreFields
        {
            get => includeOnlyCoreFields;
            set => SetValue(ref includeOnlyCoreFields, value);
        }

        public bool ShowNotificationAfterAutomaticExport
        {
            get => showNotificationAfterAutomaticExport;
            set => SetValue(ref showNotificationAfterAutomaticExport, value);
        }

        public bool ShowNotificationAfterManualExport
        {
            get => showNotificationAfterManualExport;
            set => SetValue(ref showNotificationAfterManualExport, value);
        }

        public bool IncludeLocalInstallPaths
        {
            get => includeLocalInstallPaths;
            set => SetValue(ref includeLocalInstallPaths, value);
        }

        /// <summary>Web library server. Defaults to <see cref="WebConfig.DefaultServerUrl"/>.</summary>
        public string ServerUrl
        {
            get => serverUrl;
            set => SetValue(ref serverUrl, value);
        }

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

        public List<ExportFormat> GetSelectedFormats()
        {
            var formats = new List<ExportFormat>();
            if (OutputJson) formats.Add(ExportFormat.Json);
            if (OutputText) formats.Add(ExportFormat.Text);
            if (OutputMarkdown) formats.Add(ExportFormat.Markdown);
            if (OutputCsv) formats.Add(ExportFormat.Csv);
            return formats;
        }

        public void EnsureDefaults()
        {
            if (string.IsNullOrWhiteSpace(OutputDirectory))
            {
                OutputDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "PlayniteLibraryExport");
            }

            if (string.IsNullOrWhiteSpace(OutputFileBaseName))
            {
                OutputFileBaseName = "playnite-library";
            }

            if (!GetSelectedFormats().Any())
            {
                OutputJson = true;
            }

            ServerUrl = WebConfig.NormalizeServerUrl(ServerUrl);

            if (string.IsNullOrWhiteSpace(DeviceName))
            {
                DeviceName = Environment.MachineName;
            }
        }

        public void Normalize()
        {
            OutputDirectory = Environment.ExpandEnvironmentVariables(OutputDirectory ?? string.Empty).Trim();
            OutputFileBaseName = StripKnownExtension((OutputFileBaseName ?? string.Empty).Trim());
            EnsureDefaults();
        }

        public static string StripKnownExtension(string fileName)
        {
            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(extension))
            {
                return fileName;
            }

            var knownExtensions = new[] { ".json", ".txt", ".md", ".csv" };
            return knownExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)
                ? Path.GetFileNameWithoutExtension(fileName)
                : fileName;
        }
    }

    public class PlayniteLibraryExporterSettingsViewModel : ObservableObject, ISettings
    {
        private readonly PlayniteLibraryExporter plugin;
        private PlayniteLibraryExporterSettings editingClone;
        private PlayniteLibraryExporterSettings settings;

        public PlayniteLibraryExporterSettings Settings
        {
            get => settings;
            set
            {
                settings = value;
                OnPropertyChanged();
            }
        }

        public ICommand SelectOutputDirectoryCommand { get; }
        public ICommand LinkCommand { get; }
        public ICommand CancelLinkCommand { get; }
        public ICommand UnlinkCommand { get; }
        public ICommand UploadNowCommand { get; }
        public ICommand OpenLibraryCommand { get; }
        public ICommand CopyLibraryLinkCommand { get; }
        public ICommand OpenAccountCommand { get; }

        private WebSyncService web;

        public PlayniteLibraryExporterSettingsViewModel(PlayniteLibraryExporter plugin)
        {
            this.plugin = plugin;
            SelectOutputDirectoryCommand = new RelayCommand(SelectOutputDirectory);
            LinkCommand = new RelayCommand(() => plugin.StartWebLink());
            CancelLinkCommand = new RelayCommand(() => web?.CancelLink());
            UnlinkCommand = new RelayCommand(() => plugin.UnlinkWeb());
            UploadNowCommand = new RelayCommand(() => plugin.UploadToWebNow());
            OpenLibraryCommand = new RelayCommand(() => web?.OpenLibrary());
            CopyLibraryLinkCommand = new RelayCommand(CopyLibraryLink);
            OpenAccountCommand = new RelayCommand(() => web?.OpenAccountPage());

            Settings = plugin.LoadPluginSettings<PlayniteLibraryExporterSettings>() ??
                       new PlayniteLibraryExporterSettings();
            Settings.EnsureDefaults();
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
            Settings.Normalize();
            plugin.SavePluginSettings(Settings);
            web?.RefreshPhase();
        }

        public void Save()
        {
            Settings.Normalize();
            plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = Validate(Settings);
            return errors.Count == 0;
        }

        public static List<string> Validate(PlayniteLibraryExporterSettings settings)
        {
            var errors = new List<string>();
            if (settings == null)
            {
                errors.Add("Settings could not be loaded.");
                return errors;
            }

            if (string.IsNullOrWhiteSpace(settings.OutputDirectory))
            {
                errors.Add("Output directory must not be empty.");
            }
            else
            {
                var expandedDirectory = Environment.ExpandEnvironmentVariables(settings.OutputDirectory);
                if (expandedDirectory.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                {
                    errors.Add("Output directory contains invalid path characters.");
                }
            }

            if (string.IsNullOrWhiteSpace(settings.OutputFileBaseName))
            {
                errors.Add("Output file base name must not be empty.");
            }
            else if (settings.OutputFileBaseName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                     settings.OutputFileBaseName.Contains("\\") ||
                     settings.OutputFileBaseName.Contains("/"))
            {
                errors.Add("Output file base name must be a file name only and must not contain path separators.");
            }

            if (!settings.GetSelectedFormats().Any())
            {
                errors.Add("At least one output format must be selected.");
            }

            if (!Uri.TryCreate(WebConfig.NormalizeServerUrl(settings.ServerUrl), UriKind.Absolute, out var server) ||
                (server.Scheme != Uri.UriSchemeHttps && !server.IsLoopback))
            {
                errors.Add("Web library server must be an https:// URL.");
            }

            return errors;
        }

        private void SelectOutputDirectory()
        {
            var selected = plugin.Api.Dialogs.SelectFolder();
            if (!string.IsNullOrWhiteSpace(selected))
            {
                Settings.OutputDirectory = selected;
            }
        }
    }
}
