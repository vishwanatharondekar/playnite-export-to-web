using Playnite.SDK.Data;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PlayniteLibraryExporter
{
    public class WebApiException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public string ErrorCode { get; }
        public TimeSpan? RetryAfter { get; }

        public WebApiException(HttpStatusCode statusCode, string errorCode, string message, TimeSpan? retryAfter)
            : base(message)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
            RetryAfter = retryAfter;
        }

        public bool IsUnauthorized => StatusCode == HttpStatusCode.Unauthorized;
        public bool IsRateLimited => (int)StatusCode == 429;
    }

    public class DeviceStartResponse
    {
        public string deviceCode { get; set; }
        public string userCode { get; set; }
        public string verifyUrl { get; set; }
        public int expiresIn { get; set; }
        public int interval { get; set; }
    }

    public class DeviceTokenResponse
    {
        public string deviceToken { get; set; }
        public string handle { get; set; }
        public string url { get; set; }
    }

    public class MeResponse
    {
        public string handle { get; set; }
        public string url { get; set; }
        public string exportedAt { get; set; }
        public int gameCount { get; set; }
    }

    public class UploadResponse
    {
        public bool updated { get; set; }
        public int games { get; set; }
    }

    internal class ApiErrorBody
    {
        public string error { get; set; }
        public string message { get; set; }
    }

    /// <summary>Client for the web library API (see the server's README for the contract).</summary>
    public class WebApiClient
    {
        private static readonly HttpClient http = CreateHttpClient();
        private readonly string serverUrl;

        public WebApiClient(string serverUrl)
        {
            this.serverUrl = WebConfig.NormalizeServerUrl(serverUrl);
        }

        private static HttpClient CreateHttpClient()
        {
            // .NET Framework 4.6.2 does not enable TLS 1.2 by default
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"PlayniteLibraryWeb/{version}");
            return client;
        }

        public Task<DeviceStartResponse> StartLinkAsync(string deviceName)
        {
            return SendAsync<DeviceStartResponse>(HttpMethod.Post, "/api/device/start", null, JsonContent(new { deviceName }));
        }

        /// <summary>Returns null while the user has not approved the link yet.</summary>
        public async Task<DeviceTokenResponse> PollLinkAsync(string deviceCode)
        {
            try
            {
                return await SendAsync<DeviceTokenResponse>(HttpMethod.Post, "/api/device/token", null, JsonContent(new { deviceCode }));
            }
            catch (WebApiException ex) when (ex.ErrorCode == "authorization_pending")
            {
                return null;
            }
        }

        public Task<MeResponse> GetMeAsync(string token)
        {
            return SendAsync<MeResponse>(HttpMethod.Get, "/api/me", token, null);
        }

        public Task<UploadResponse> UploadExportAsync(string token, string exportJson)
        {
            var content = new ByteArrayContent(Gzip(exportJson));
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            content.Headers.ContentEncoding.Add("gzip");
            return SendAsync<UploadResponse>(HttpMethod.Put, "/api/me/export", token, content);
        }

        public Task UnlinkAsync(string token)
        {
            return SendAsync<object>(HttpMethod.Delete, "/api/me/device", token, null);
        }

        private async Task<T> SendAsync<T>(HttpMethod method, string path, string token, HttpContent content) where T : class
        {
            using (var request = new HttpRequestMessage(method, serverUrl + path) { Content = content })
            {
                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                using (var response = await http.SendAsync(request).ConfigureAwait(false))
                {
                    var body = response.Content == null ? string.Empty : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        throw ToException(response, body);
                    }

                    return string.IsNullOrWhiteSpace(body) ? null : Serialization.FromJson<T>(body);
                }
            }
        }

        private static WebApiException ToException(HttpResponseMessage response, string body)
        {
            ApiErrorBody error = null;
            try
            {
                error = Serialization.FromJson<ApiErrorBody>(body);
            }
            catch
            {
                // Not a JSON error body (e.g. a proxy error page)
            }

            var retryAfter = response.Headers.RetryAfter?.Delta;
            var message = error?.message ?? error?.error ?? $"Server returned HTTP {(int)response.StatusCode}.";
            return new WebApiException(response.StatusCode, error?.error, message, retryAfter);
        }

        private static StringContent JsonContent(object value)
        {
            return new StringContent(Serialization.ToJson(value), Encoding.UTF8, "application/json");
        }

        private static byte[] Gzip(string text)
        {
            var bytes = new UTF8Encoding(false).GetBytes(text);
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionLevel.Optimal, true))
                {
                    gzip.Write(bytes, 0, bytes.Length);
                }

                return output.ToArray();
            }
        }
    }
}
