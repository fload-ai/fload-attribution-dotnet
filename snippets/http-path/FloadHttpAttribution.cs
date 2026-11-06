// Fload install attribution over plain HTTP, for apps that can't use the
// Fload.Attribution package (classic Xamarin.iOS / Xamarin.Android).
// Written for C# 7.3 and netstandard2.0, so it compiles in a classic Xamarin
// project. MIT licensed: copy it into your app.
//
// It reports the install once, with Apple's AdServices token or Google Play's
// Install Referrer (FloadIosSource.cs / FloadAndroidSource.cs read them), and
// your app user id, over Fload's ingest API:
//   POST {endpoint}/attribution/v1/installs
//   POST {endpoint}/attribution/v1/identify
// with the write key in X-Fload-Write-Key. Each request waits in a file until
// Fload accepts it, and a retry repeats the same requestId, so Fload counts it
// once. Call FlushAsync on every launch and when the app returns to the
// foreground. The token lives 24 hours: start this on the first launch.

using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Fload.HttpAttribution
{
    /// <summary>What the phone knows about the app and itself. Every field but AppId is optional.</summary>
    public sealed class FloadAppFacts
    {
        public string Platform;      // "ios" or "android"
        public string AppId;         // bundle id or package name
        public string AppVersion;
        public string AppBuild;
        public string OsVersion;
        public string DeviceModel;
        public string Locale;
    }

    /// <summary>The install's source: Apple's token (iOS) or the Play Install Referrer (Android).</summary>
    public sealed class FloadInstallSource
    {
        // iOS: "token", "unsupported_os" or "token_error". Android: "ok", "unavailable", "not_supported" or "error".
        public string Status;
        public string AppleToken;               // iOS, only with Status "token"
        public DateTimeOffset? AppleTokenCreatedAt;
        public string PlayReferrer;             // Android, the raw referrer string
        public DateTimeOffset? PlayReferrerClickAt;
        public DateTimeOffset? PlayInstallBeginAt;
        public bool? PlayInstant;
    }

    public sealed class FloadHttpAttribution
    {
        public const string SdkName = "fload-attribution-dotnet-http";
        public const string SdkVersion = "1.0.0";
        public static readonly Uri ProductionEndpoint = new Uri("https://ingest.fload.com");

        // Apple's token lives 24 hours. After 23 the install goes without it.
        static readonly TimeSpan TokenCutoff = TimeSpan.FromHours(23);

        readonly string writeKey;
        readonly string directory;
        readonly Uri endpoint;
        readonly HttpClient http;
        readonly Func<DateTimeOffset> now;
        readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);    // one flush at a time
        readonly SemaphoreSlim state = new SemaphoreSlim(1, 1);   // recording the install vs. a new user id

        /// <param name="writeKey">The app's public write key from Fload (fload_pk_...).</param>
        /// <param name="stateDirectory">A private directory that is not backed up (iOS: Library/Application Support
        /// marked excluded from backup; Android: Context.NoBackupFilesDir).</param>
        /// <param name="endpoint">Null for production. Only for tests against your own API.</param>
        public FloadHttpAttribution(string writeKey, string stateDirectory, Uri endpoint = null,
            HttpMessageHandler handler = null, Func<DateTimeOffset> clock = null)
        {
            if (writeKey == null || !writeKey.StartsWith("fload_pk_", StringComparison.Ordinal))
                throw new ArgumentException("A Fload write key starts with fload_pk_.", "writeKey");
            if (stateDirectory == null) throw new ArgumentNullException("stateDirectory");
            this.writeKey = writeKey;
            directory = Path.Combine(stateDirectory, "fload-attribution");
            Directory.CreateDirectory(directory);
            var root = (endpoint ?? ProductionEndpoint).AbsoluteUri;
            this.endpoint = new Uri(root.EndsWith("/", StringComparison.Ordinal) ? root : root + "/");
            http = handler == null ? new HttpClient() : new HttpClient(handler);
            http.Timeout = TimeSpan.FromSeconds(15);
            http.DefaultRequestHeaders.UserAgent.ParseAdd(SdkName + "/" + SdkVersion);
            now = clock ?? (() => DateTimeOffset.UtcNow);
            InstallReference = ReadOrCreate("install-reference", () => Guid.NewGuid().ToString());
        }

        /// <summary>This install's random id. A reinstall gets a new one.</summary>
        public string InstallReference { get; private set; }

        /// <summary>True once the install has been queued (on this install's first launch).</summary>
        public bool InstallRecorded { get { return File.Exists(PathOf("install-recorded")); } }

        /// <summary>True after Fload answered 410: the key is revoked and nothing is sent again.</summary>
        public bool Stopped { get { return File.Exists(PathOf("stopped")); } }

        /// <summary>
        /// On the first launch, queues the install with its source, then sends what is queued.
        /// Later launches only send. readSource runs once per install.
        /// </summary>
        public async Task StartAsync(FloadAppFacts facts, Func<Task<FloadInstallSource>> readSource)
        {
            if (facts == null) throw new ArgumentNullException("facts");
            if (!InstallRecorded)
            {
                var firstLaunchAt = ReadOrCreate("first-launch-at", () => Iso(now()));
                var source = await readSource().ConfigureAwait(false);
                // Under the state lock, so a user id set meanwhile rides with the install or follows it.
                await state.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (!InstallRecorded)
                    {
                        var userId = ReadText("user-id");
                        WriteText("pending-install.json", InstallJson(NewId(), facts, firstLaunchAt, source, userId));
                        if (userId != null) WriteText("sent-user-id", userId);
                        WriteText("install-recorded", Iso(now()));
                    }
                }
                finally
                {
                    state.Release();
                }
            }
            await FlushAsync().ConfigureAwait(false);
        }

        /// <summary>Links your app user id (e.g. RevenueCat's) to this install. The same id is sent once.</summary>
        public async Task SetUserIdAsync(string userId)
        {
            if (userId == null) return;
            userId = userId.Trim();
            if (userId.Length == 0 || userId.Length > 256) return;
            await state.WaitAsync().ConfigureAwait(false);
            try
            {
                WriteText("user-id", userId);
                if (InstallRecorded && ReadText("sent-user-id") != userId)
                {
                    var body = "{" + Prop("requestId", NewId()) + "," + Prop("installReference", InstallReference) + ","
                        + Prop("platform", ReadPlatform()) + "," + Prop("appUserId", userId) + "}";
                    WriteText("pending-identify-" + NewId() + ".json", body);
                    WriteText("sent-user-id", userId);
                }
            }
            finally
            {
                state.Release();
            }
            await FlushAsync().ConfigureAwait(false);
        }

        /// <summary>Sends everything queued, install first. Safe to call often.</summary>
        public async Task FlushAsync()
        {
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var install = PathOf("pending-install.json");
                if (File.Exists(install))
                {
                    if (!await SendAsync("attribution/v1/installs", install).ConfigureAwait(false)) return;
                }
                foreach (var identify in Directory.GetFiles(directory, "pending-identify-*.json"))
                {
                    if (!await SendAsync("attribution/v1/identify", identify).ConfigureAwait(false)) return;
                }
            }
            finally
            {
                gate.Release();
            }
        }

        // Returns false when the rest of the queue should wait (no network, 401, 429, 5xx, 410).
        async Task<bool> SendAsync(string path, string file)
        {
            if (Stopped) return false;
            var body = File.ReadAllText(file, Encoding.UTF8);
            var isInstall = path.EndsWith("installs", StringComparison.Ordinal);
            if (isInstall) body = ExpireOldToken(file, body);
            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(endpoint, path));
            request.Headers.Add("X-Fload-Write-Key", writeKey);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(request).ConfigureAwait(false);
            }
            catch (HttpRequestException) { return false; }
            catch (TaskCanceledException) { return false; }
            using (response)
            {
                var status = (int)response.StatusCode;
                if ((status >= 200 && status < 300) || status == 400 || status == 413)
                {
                    // 2xx: Fload has it. 400 or 413: it can never be valid, so drop it.
                    File.Delete(file);
                    if (isInstall) { DeleteState("pending-install-expired.json"); DeleteState("token-created-at"); }
                    return true;
                }
                if (status == 410) { WriteText("stopped", Iso(now())); return false; }      // key revoked: stop for good
                return false;                                                                 // 401, 429, 5xx: try on the next flush
            }
        }

        // Apple's token is useless after 24 hours: send the install without it, as token_expired.
        string ExpireOldToken(string file, string body)
        {
            var createdAt = ReadText("token-created-at");
            if (createdAt == null) return body;
            var created = DateTimeOffset.Parse(createdAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
            if (now() - created < TokenCutoff) return body;
            var expired = ReadText("pending-install-expired.json");
            if (expired == null) return body;
            File.WriteAllText(file, expired, Encoding.UTF8);
            DeleteState("token-created-at");
            DeleteState("pending-install-expired.json");
            return expired;
        }

        string InstallJson(string requestId, FloadAppFacts facts, string firstLaunchAt, FloadInstallSource source, string userId)
        {
            var platform = facts.Platform == "android" ? "android" : "ios";
            WriteText("platform", platform);
            Func<string, string> build = sourceJson =>
            {
                var b = new StringBuilder("{");
                b.Append(Prop("requestId", requestId)).Append(',');
                b.Append(Prop("installReference", InstallReference)).Append(',');
                b.Append(Prop("platform", platform)).Append(',');
                b.Append(Prop("appId", Cut(facts.AppId, 256) ?? "unknown"));
                AppendOptional(b, "appVersion", Cut(facts.AppVersion, 64));
                AppendOptional(b, "appBuild", Cut(facts.AppBuild, 64));
                AppendOptional(b, "osVersion", Cut(facts.OsVersion, 64));
                AppendOptional(b, "deviceModel", Cut(facts.DeviceModel, 64));
                AppendOptional(b, "locale", Cut(facts.Locale, 64));
                b.Append(",\"sdk\":{").Append(Prop("name", SdkName)).Append(',').Append(Prop("version", SdkVersion)).Append('}');
                b.Append(',').Append(Prop("firstLaunchAt", firstLaunchAt));
                b.Append(',').Append(sourceJson);
                AppendOptional(b, "appUserId", userId);
                return b.Append('}').ToString();
            };
            source = source ?? new FloadInstallSource { Status = platform == "ios" ? "token_error" : "error" };
            if (platform == "android") return build("\"playReferrer\":" + PlayJson(source));
            var hasToken = source.Status == "token" && !string.IsNullOrEmpty(source.AppleToken);
            if (!hasToken)
            {
                var status = source.Status == "unsupported_os" ? "unsupported_os" : "token_error";
                return build("\"appleAds\":{" + Prop("status", status) + "}");
            }
            var tokenCreatedAt = source.AppleTokenCreatedAt ?? now();
            WriteText("token-created-at", Iso(tokenCreatedAt));
            WriteText("pending-install-expired.json", build("\"appleAds\":{" + Prop("status", "token_expired") + "}"));
            return build("\"appleAds\":{" + Prop("status", "token") + "," + Prop("token", source.AppleToken) + ","
                + Prop("tokenCreatedAt", Iso(tokenCreatedAt)) + "}");
        }

        static string PlayJson(FloadInstallSource source)
        {
            var status = source.Status == "ok" || source.Status == "unavailable" || source.Status == "not_supported"
                ? source.Status : "error";
            var b = new StringBuilder("{").Append(Prop("status", status));
            if (status == "ok")
            {
                AppendOptional(b, "referrer", Cut(source.PlayReferrer, 1024));
                if (source.PlayReferrerClickAt.HasValue) AppendOptional(b, "referrerClickAt", Iso(source.PlayReferrerClickAt.Value));
                if (source.PlayInstallBeginAt.HasValue) AppendOptional(b, "installBeginAt", Iso(source.PlayInstallBeginAt.Value));
                if (source.PlayInstant.HasValue) b.Append(",\"googlePlayInstant\":").Append(source.PlayInstant.Value ? "true" : "false");
            }
            return b.Append('}').ToString();
        }

        string ReadPlatform() { return ReadText("platform") ?? "ios"; }

        static string NewId() { return Guid.NewGuid().ToString(); }

        /// <summary>ISO 8601 in UTC with milliseconds.</summary>
        public static string Iso(DateTimeOffset at)
        {
            return at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        }

        static string Cut(string value, int max)
        {
            if (value == null) return null;
            value = value.Trim();
            if (value.Length == 0) return null;
            if (value.Length <= max) return value;
            var cut = value.Substring(0, max);
            return char.IsHighSurrogate(cut[cut.Length - 1]) ? cut.Substring(0, max - 1) : cut;
        }

        static void AppendOptional(StringBuilder b, string name, string value)
        {
            if (value != null) b.Append(',').Append(Prop(name, value));
        }

        static string Prop(string name, string value) { return Quote(name) + ":" + Quote(value); }

        static string Quote(string value)
        {
            var b = new StringBuilder("\"");
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': b.Append("\\\""); break;
                    case '\\': b.Append("\\\\"); break;
                    case '\n': b.Append("\\n"); break;
                    case '\r': b.Append("\\r"); break;
                    case '\t': b.Append("\\t"); break;
                    default:
                        if (c < 0x20) b.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else b.Append(c);
                        break;
                }
            }
            return b.Append('"').ToString();
        }

        string PathOf(string name) { return Path.Combine(directory, name); }

        void DeleteState(string name)
        {
            if (File.Exists(PathOf(name))) File.Delete(PathOf(name));
        }

        string ReadText(string name)
        {
            var file = PathOf(name);
            return File.Exists(file) ? File.ReadAllText(file, Encoding.UTF8) : null;
        }

        void WriteText(string name, string value)
        {
            var temp = PathOf(name + ".tmp");
            File.WriteAllText(temp, value, Encoding.UTF8);
            if (File.Exists(PathOf(name))) File.Delete(PathOf(name));
            File.Move(temp, PathOf(name));
        }

        string ReadOrCreate(string name, Func<string> make)
        {
            var value = ReadText(name);
            if (value != null) return value;
            value = make();
            WriteText(name, value);
            return value;
        }
    }
}
