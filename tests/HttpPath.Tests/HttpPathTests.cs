using System.Net;
using System.Text.Json;
using Fload.HttpAttribution;
using Xunit;

namespace HttpPath.Tests;

public sealed class HttpPathTests : IDisposable
{
    const string Key = "fload_pk_test_local";
    readonly string dir = Path.Combine(Path.GetTempPath(), "fload-http-" + Guid.NewGuid());
    readonly FakeIngest ingest = new();
    DateTimeOffset clock = new(2026, 11, 6, 10, 0, 0, TimeSpan.Zero);

    static readonly FloadAppFacts IosFacts = new()
    {
        Platform = "ios", AppId = "com.example.app", AppVersion = "2.3.0", AppBuild = "231",
        OsVersion = "26.4", DeviceModel = "iPhone18,3", Locale = "de_DE",
    };

    static readonly FloadAppFacts AndroidFacts = new()
    {
        Platform = "android", AppId = "com.example.app", AppVersion = "2.3.0", AppBuild = "231",
        OsVersion = "15", DeviceModel = "Google Pixel 9", Locale = "de-DE",
    };

    FloadHttpAttribution Make() => new(Key, dir, new Uri("http://localhost:3901"), ingest, () => clock);

    static Func<Task<FloadInstallSource>> Token(string token = "QUJDRA==") =>
        () => Task.FromResult(new FloadInstallSource { Status = "token", AppleToken = token, AppleTokenCreatedAt = new DateTimeOffset(2026, 11, 6, 10, 0, 1, TimeSpan.Zero) });

    public void Dispose() { if (Directory.Exists(dir)) Directory.Delete(dir, true); }

    [Fact]
    public async Task First_launch_sends_the_install_once_with_the_token()
    {
        await Make().StartAsync(IosFacts, Token());
        await Make().StartAsync(IosFacts, () => throw new InvalidOperationException("read once per install"));

        var install = Assert.Single(ingest.Requests);
        Assert.Equal("/attribution/v1/installs", install.Path);
        Assert.Equal(Key, install.Key);
        var body = install.Body;
        Assert.Equal("ios", body.GetProperty("platform").GetString());
        Assert.Equal("com.example.app", body.GetProperty("appId").GetString());
        Assert.Equal("fload-attribution-dotnet-http", body.GetProperty("sdk").GetProperty("name").GetString());
        Assert.Equal("2026-11-06T10:00:00.000Z", body.GetProperty("firstLaunchAt").GetString());
        var apple = body.GetProperty("appleAds");
        Assert.Equal("token", apple.GetProperty("status").GetString());
        Assert.Equal("QUJDRA==", apple.GetProperty("token").GetString());
        Assert.Equal("2026-11-06T10:00:01.000Z", apple.GetProperty("tokenCreatedAt").GetString());
        Assert.False(body.TryGetProperty("playReferrer", out _));
    }

    [Fact]
    public async Task A_user_id_set_before_the_install_rides_with_it_and_a_later_one_is_an_identify()
    {
        var sdk = Make();
        await sdk.SetUserIdAsync("$RCAnonymousID:abc");
        await sdk.StartAsync(IosFacts, Token());
        await sdk.SetUserIdAsync("$RCAnonymousID:abc");   // same id: nothing new
        await sdk.SetUserIdAsync("user_123");

        Assert.Equal(2, ingest.Requests.Count);
        Assert.Equal("$RCAnonymousID:abc", ingest.Requests[0].Body.GetProperty("appUserId").GetString());
        var identify = ingest.Requests[1];
        Assert.Equal("/attribution/v1/identify", identify.Path);
        Assert.Equal("user_123", identify.Body.GetProperty("appUserId").GetString());
        Assert.Equal(sdk.InstallReference, identify.Body.GetProperty("installReference").GetString());
        Assert.Equal("ios", identify.Body.GetProperty("platform").GetString());
    }

    [Fact]
    public async Task A_user_id_set_while_the_source_is_read_is_never_lost()
    {
        var sdk = Make();
        var reading = new TaskCompletionSource<FloadInstallSource>();
        var start = sdk.StartAsync(IosFacts, () => reading.Task);
        await sdk.SetUserIdAsync("user_123");   // the install is not recorded yet
        reading.SetResult(new FloadInstallSource { Status = "token_error" });
        await start;

        var install = Assert.Single(ingest.Requests);
        Assert.Equal("user_123", install.Body.GetProperty("appUserId").GetString());
    }

    [Fact]
    public async Task A_retry_repeats_the_same_request_id()
    {
        ingest.Offline = true;
        await Make().StartAsync(IosFacts, Token());
        ingest.Offline = false;
        ingest.Answer = _ => (HttpStatusCode)503;
        await Make().FlushAsync();
        ingest.Answer = _ => HttpStatusCode.Accepted;
        await Make().FlushAsync();
        await Make().FlushAsync();

        Assert.Equal(2, ingest.Requests.Count);
        Assert.Equal(ingest.Requests[0].Body.GetProperty("requestId").GetString(), ingest.Requests[1].Body.GetProperty("requestId").GetString());
    }

    [Fact]
    public async Task An_unknown_key_keeps_the_queue()
    {
        ingest.Answer = _ => HttpStatusCode.Unauthorized;
        await Make().StartAsync(IosFacts, Token());
        ingest.Answer = _ => HttpStatusCode.Accepted;
        await Make().FlushAsync();

        Assert.Equal(2, ingest.Requests.Count);
        Assert.Equal("/attribution/v1/installs", ingest.Requests[1].Path);
    }

    [Fact]
    public async Task A_revoked_key_stops_for_good()
    {
        ingest.Answer = _ => HttpStatusCode.Gone;
        var sdk = Make();
        await sdk.StartAsync(IosFacts, Token());
        ingest.Answer = _ => HttpStatusCode.Accepted;
        await sdk.SetUserIdAsync("user_123");
        await Make().FlushAsync();

        Assert.Single(ingest.Requests);
        Assert.True(sdk.Stopped);
    }

    [Fact]
    public async Task A_malformed_request_is_dropped()
    {
        ingest.Answer = _ => HttpStatusCode.BadRequest;
        await Make().StartAsync(IosFacts, Token());
        ingest.Answer = _ => HttpStatusCode.Accepted;
        await Make().FlushAsync();

        Assert.Single(ingest.Requests);
    }

    [Fact]
    public async Task After_23_hours_the_install_goes_without_the_token()
    {
        ingest.Offline = true;
        await Make().StartAsync(IosFacts, Token());
        clock = clock.AddHours(23).AddMinutes(1);
        ingest.Offline = false;
        await Make().FlushAsync();

        var apple = Assert.Single(ingest.Requests).Body.GetProperty("appleAds");
        Assert.Equal("token_expired", apple.GetProperty("status").GetString());
        Assert.False(apple.TryGetProperty("token", out _));
    }

    [Fact]
    public async Task Without_a_token_the_install_says_why()
    {
        await Make().StartAsync(IosFacts, () => Task.FromResult(new FloadInstallSource { Status = "unsupported_os" }));

        var apple = Assert.Single(ingest.Requests).Body.GetProperty("appleAds");
        Assert.Equal("unsupported_os", apple.GetProperty("status").GetString());
        Assert.False(apple.TryGetProperty("token", out _));
    }

    [Fact]
    public async Task Android_sends_the_play_referrer()
    {
        await Make().StartAsync(AndroidFacts, () => Task.FromResult(new FloadInstallSource
        {
            Status = "ok",
            PlayReferrer = "utm_source=google-play&utm_medium=organic",
            PlayReferrerClickAt = DateTimeOffset.FromUnixTimeSeconds(1_793_000_000),
            PlayInstant = false,
        }));

        var body = Assert.Single(ingest.Requests).Body;
        Assert.Equal("android", body.GetProperty("platform").GetString());
        Assert.False(body.TryGetProperty("appleAds", out _));
        var play = body.GetProperty("playReferrer");
        Assert.Equal("ok", play.GetProperty("status").GetString());
        Assert.Equal("utm_source=google-play&utm_medium=organic", play.GetProperty("referrer").GetString());
        Assert.Equal("2026-10-26T07:33:20.000Z", play.GetProperty("referrerClickAt").GetString());
        Assert.False(play.GetProperty("googlePlayInstant").GetBoolean());
    }

    [Fact]
    public async Task Text_is_escaped_and_cut_to_the_contract_limits()
    {
        var facts = new FloadAppFacts { Platform = "ios", AppId = "com.example.\"app\"\\", DeviceModel = new string('x', 80) };
        await Make().StartAsync(facts, Token());

        var body = Assert.Single(ingest.Requests).Body;
        Assert.Equal("com.example.\"app\"\\", body.GetProperty("appId").GetString());
        Assert.Equal(64, body.GetProperty("deviceModel").GetString()!.Length);
        Assert.False(body.TryGetProperty("appVersion", out _));
    }

    [Fact]
    public void A_key_that_is_not_a_write_key_is_refused()
    {
        Assert.Throws<ArgumentException>(() => new FloadHttpAttribution("fload_sk_secret", dir));
    }
}
