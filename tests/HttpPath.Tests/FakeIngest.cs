using System.Net;
using System.Text;
using System.Text.Json;

namespace HttpPath.Tests;

/// <summary>An in-process ingest that answers with scripted status codes and records each request.</summary>
sealed class FakeIngest : HttpMessageHandler
{
    public readonly List<(string Path, string Key, JsonElement Body)> Requests = new();
    public Func<string, HttpStatusCode> Answer = _ => HttpStatusCode.Accepted;
    public bool Offline;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (Offline) throw new HttpRequestException("offline");
        var body = await request.Content!.ReadAsStringAsync(cancellationToken);
        var key = request.Headers.GetValues("X-Fload-Write-Key").Single();
        var path = request.RequestUri!.AbsolutePath;
        Requests.Add((path, key, JsonDocument.Parse(body).RootElement.Clone()));
        return new HttpResponseMessage(Answer(path)) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
    }
}
