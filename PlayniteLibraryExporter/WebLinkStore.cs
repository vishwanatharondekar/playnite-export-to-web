using Playnite.SDK.Data;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace PlayniteLibraryExporter
{
    /// <summary>Link state persisted in the plugin's data folder.</summary>
    public class WebLinkState
    {
        /// <summary>Device token, encrypted for the current Windows user (DPAPI), base64.</summary>
        public string ProtectedToken { get; set; }
        public string Handle { get; set; }
        public string LibraryUrl { get; set; }
        public DateTime? LastUploadUtc { get; set; }
        public string LastUploadedHash { get; set; }
    }

    /// <summary>
    /// Keeps the link outside the regular plugin settings: Playnite restores settings when the
    /// settings window is cancelled, which must not undo a link completed while it was open.
    /// </summary>
    public class WebLinkStore
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("PlayniteLibraryWeb.DeviceToken");
        private readonly string path;
        private readonly object sync = new object();
        private WebLinkState state;

        public WebLinkStore(string dataDirectory)
        {
            path = Path.Combine(dataDirectory, "web-link.json");
            state = Load();
        }

        public WebLinkState State
        {
            get { lock (sync) return Serialization.GetClone(state); }
        }

        /// <summary>Device token, or null when this PC is not linked.</summary>
        public string GetToken()
        {
            lock (sync)
            {
                if (string.IsNullOrEmpty(state.ProtectedToken))
                {
                    return null;
                }

                try
                {
                    var bytes = ProtectedData.Unprotect(Convert.FromBase64String(state.ProtectedToken), Entropy, DataProtectionScope.CurrentUser);
                    return Encoding.UTF8.GetString(bytes);
                }
                catch (Exception)
                {
                    return null; // e.g. the file was copied from another Windows account
                }
            }
        }

        public void SaveLink(string token, string handle, string libraryUrl)
        {
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(token), Entropy, DataProtectionScope.CurrentUser);
            Update(s =>
            {
                s.ProtectedToken = Convert.ToBase64String(bytes);
                s.Handle = handle;
                s.LibraryUrl = libraryUrl;
                s.LastUploadUtc = null;
                s.LastUploadedHash = null;
            });
        }

        public void Clear()
        {
            Update(s =>
            {
                s.ProtectedToken = null;
                s.Handle = null;
                s.LibraryUrl = null;
                s.LastUploadUtc = null;
                s.LastUploadedHash = null;
            });
        }

        public void Update(Action<WebLinkState> change)
        {
            lock (sync)
            {
                change(state);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, Serialization.ToJson(state, true), new UTF8Encoding(false));
            }
        }

        private WebLinkState Load()
        {
            try
            {
                if (File.Exists(path))
                {
                    return Serialization.FromJson<WebLinkState>(File.ReadAllText(path)) ?? new WebLinkState();
                }
            }
            catch (Exception)
            {
                // Corrupt file: start unlinked
            }

            return new WebLinkState();
        }
    }
}
