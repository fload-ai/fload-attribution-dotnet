using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Fload.HttpAttribution;
using Xunit;

namespace HttpPath.Tests;

/// <summary>
/// Sends real HTTP to tools/ingest-stub/stub.py, which checks each request against
/// Fload's wire contract and answers 400 for anything it would refuse.
/// </summary>
public sealed class StubContractTests : IAsyncLifetime
{
    const string Key = "fload_pk_test_local";
    Process? stub;
    Uri endpoint = null!;
    readonly string dir = Path.Combine(Path.GetTempPath(), "fload-http-stub-" + Guid.NewGuid());

    public async Task InitializeAsync()
    {
        var port = FreePort();
        endpoint = new Uri($"http://127.0.0.1:{port}");
        var script = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../tools/ingest-stub/stub.py"));
        stub = Process.Start(new ProcessStartInfo("python3", $"-I \"{script}\" --port {port} --key {Key} --revoked fload_pk_revoked")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
        stub.OutputDataReceived += (_, _) => { };
        stub.ErrorDataReceived += (_, _) => { };
        stub.BeginOutputReadLine();
        stub.BeginErrorReadLine();
        using var http = new HttpClient();
        for (var i = 0; i < 50; i++)
        {
            try { await http.GetAsync(new Uri(endpoint, "__stub__/requests")); return; }
            catch (HttpRequestException) { await Task.Delay(100); }
        }
        throw new InvalidOperationException("the ingest stub did not start");
    }

    public Task DisposeAsync()
    {
        if (stub is { HasExited: false })
        {
            stub.Kill(entireProcessTree: true);
            stub.WaitForExit(5000);
        }
        stub?.Dispose();
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task iOS_and_Android_installs_and_an_identify_pass_the_contract()
    {
        var ios = new FloadHttpAttribution(Key, Path.Combine(dir, "ios"), endpoint);
        await ios.StartAsync(new FloadAppFacts { Platform = "ios", AppId = "com.example.app", AppVersion = "1.0", Locale = "en_US" },
            () => Task.FromResult(new FloadInstallSource { Status = "token", AppleToken = "QUJDRA==", AppleTokenCreatedAt = DateTimeOffset.UtcNow }));
        await ios.SetUserIdAsync("$RCAnonymousID:abc");

        var android = new FloadHttpAttribution(Key, Path.Combine(dir, "android"), endpoint);
        await android.StartAsync(new FloadAppFacts { Platform = "android", AppId = "com.example.app", OsVersion = "15" },
            () => Task.FromResult(new FloadInstallSource { Status = "ok", PlayReferrer = "utm_source=google-play&utm_medium=organic", PlayInstant = false }));

        var unsupported = new FloadHttpAttribution(Key, Path.Combine(dir, "old-ios"), endpoint);
        await unsupported.StartAsync(new FloadAppFacts { Platform = "ios", AppId = "com.example.app" },
            () => Task.FromResult(new FloadInstallSource { Status = "unsupported_os" }));

        var seen = await Requests();
        Assert.Equal(new[] { 202, 202, 202, 202 }, seen.Select(r => r.GetProperty("status").GetInt32()));
        Assert.Equal(
            new[] { "/attribution/v1/installs", "/attribution/v1/identify", "/attribution/v1/installs", "/attribution/v1/installs" },
            seen.Select(r => r.GetProperty("path").GetString()));
    }

    [Fact]
    public async Task A_revoked_key_gets_410_and_stops()
    {
        var sdk = new FloadHttpAttribution("fload_pk_revoked", Path.Combine(dir, "revoked"), endpoint);
        await sdk.StartAsync(new FloadAppFacts { Platform = "ios", AppId = "com.example.app" },
            () => Task.FromResult(new FloadInstallSource { Status = "token_error" }));
        await sdk.FlushAsync();

        Assert.True(sdk.Stopped);
        Assert.Equal(410, Assert.Single(await Requests()).GetProperty("status").GetInt32());
    }

    async Task<List<JsonElement>> Requests()
    {
        using var http = new HttpClient();
        var json = await http.GetStringAsync(new Uri(endpoint, "__stub__/requests"));
        return JsonDocument.Parse(json).RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    }

    static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
