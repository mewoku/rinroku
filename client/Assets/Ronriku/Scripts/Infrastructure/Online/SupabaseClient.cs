using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Ronriku.Infrastructure.Online
{
    /// <summary>Failure from the backend. <see cref="Code"/> is the stable snake_case error (e.g. insufficient_shards).</summary>
    public sealed class OnlineException : Exception
    {
        public string Code { get; }
        public long Status { get; }

        public OnlineException(string code, long status, string detail = null) : base($"{code} ({status}) {detail}")
        {
            Code = code;
            Status = status;
        }
    }

    [Serializable]
    public sealed class OnlineConfig
    {
        public string url;
        public string anonKey;
        public int timeoutSeconds = 6;
        /// <summary>
        /// Backend for the WebGL build. "same-origin" = the page's own origin (the site proxies the
        /// Supabase API paths, see deploy/Caddyfile), so the browser never needs a cross-origin call.
        /// </summary>
        public string webUrl = "same-origin";

        public static OnlineConfig Load()
        {
            var asset = Resources.Load<TextAsset>("ronriku-online");
            if (asset == null) return null;
            var config = JsonUtility.FromJson<OnlineConfig>(asset.text);
            if (Application.platform == RuntimePlatform.WebGLPlayer && !string.IsNullOrEmpty(config.webUrl))
                config.url = config.webUrl == "same-origin" ? Origin(Application.absoluteURL) ?? config.url : config.webUrl;
            return config;
        }

        private static string Origin(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
            return uri.GetLeftPart(UriPartial.Authority);
        }
    }

    /// <summary>
    /// Minimal Supabase client over UnityWebRequest (works on Android and WebGL): anonymous auth with a
    /// persisted, refreshed session, PostgREST RPC calls and reads. All calls run on the main thread.
    /// </summary>
    public sealed class SupabaseClient
    {
        private const string SessionKey = "ronriku.session";
        private readonly OnlineConfig _config;
        private Session _session;

        [Serializable]
        private sealed class Session
        {
            public string access_token;
            public string refresh_token;
            public long expires_at;
            public string user_id;
        }

        public SupabaseClient(OnlineConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            string saved = PlayerPrefs.GetString(SessionKey, string.Empty);
            if (!string.IsNullOrEmpty(saved)) _session = JsonUtility.FromJson<Session>(saved);
        }

        public string UserId => _session?.user_id;

        /// <summary>Valid access token: reuses, refreshes, or signs in anonymously.</summary>
        public async Task EnsureSessionAsync()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (_session != null && _session.expires_at - 60 > now) return;
            if (_session?.refresh_token != null)
            {
                try
                {
                    Store(await Send("POST", "/auth/v1/token?grant_type=refresh_token",
                        new JObject { ["refresh_token"] = _session.refresh_token }, false));
                    return;
                }
                catch (OnlineException e) when (e.Status == 400 || e.Status == 401)
                {
                    _session = null; // refresh token revoked: start a new anonymous identity
                }
            }
            Store(await Send("POST", "/auth/v1/signup", new JObject { ["data"] = new JObject() }, false));
        }

        public async Task<JToken> Rpc(string function, JObject args = null)
        {
            await EnsureSessionAsync();
            return await Send("POST", "/rest/v1/rpc/" + function, args ?? new JObject(), true);
        }

        /// <summary>PostgREST read, e.g. Select("figures", "select=id,seed,tier&amp;owner_id=eq.X").</summary>
        public async Task<JArray> Select(string table, string query)
        {
            await EnsureSessionAsync();
            return (JArray)await Send("GET", $"/rest/v1/{table}?{query}", null, true);
        }

        public void SignOut()
        {
            _session = null;
            PlayerPrefs.DeleteKey(SessionKey);
        }

        private void Store(JToken auth)
        {
            _session = new Session
            {
                access_token = (string)auth["access_token"],
                refresh_token = (string)auth["refresh_token"],
                expires_at = auth["expires_at"]?.Value<long>() ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (auth["expires_in"]?.Value<long>() ?? 3600),
                user_id = (string)auth["user"]?["id"]
            };
            PlayerPrefs.SetString(SessionKey, JsonUtility.ToJson(_session));
            PlayerPrefs.Save();
        }

        private async Task<JToken> Send(string method, string path, JObject body, bool authorised)
        {
            using var request = new UnityWebRequest(_config.url.TrimEnd('/') + path, method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = _config.timeoutSeconds
            };
            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString(Newtonsoft.Json.Formatting.None)));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            request.SetRequestHeader("apikey", _config.anonKey);
            request.SetRequestHeader("Authorization", "Bearer " + (authorised && _session != null ? _session.access_token : _config.anonKey));

            await request.SendWebRequest();

            string text = request.downloadHandler.text;
            if (request.result == UnityWebRequest.Result.ConnectionError)
                throw new OnlineException("offline", 0, request.error);
            if (request.responseCode >= 400)
            {
                string code = "http_" + request.responseCode;
                try
                {
                    JToken error = JToken.Parse(text);
                    code = (string)error["message"] ?? (string)error["error_code"] ?? (string)error["msg"] ?? code;
                }
                catch (Newtonsoft.Json.JsonException) { }
                throw new OnlineException(code, request.responseCode, text.Length > 200 ? text.Substring(0, 200) : text);
            }
            return string.IsNullOrWhiteSpace(text) ? JValue.CreateNull() : JToken.Parse(text);
        }
    }
}
