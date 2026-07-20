using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Extensibility;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Test;

[TestFixture]
public class SourceContextTests
{
    private sealed class CountingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public CountingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        public int Requests { get; private set; }

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests++;
            LastRequest = request;
            return Task.FromResult(_respond(request));
        }
    }

    private static SourceContext Context(TempRoot root, HttpMessageHandler handler, bool offline = false, string? token = null)
        => new(
            new HttpClient(handler),
            token,
            Path.Combine(root.Path, "cache"),
            PlatformTriple.Current,
            NullLogger.Instance,
            offline);

    private sealed record Payload(string Value);

    [Test]
    public async Task CachedJson_Within_Ttl_Skips_Network()
    {
        using TempRoot root = new();
        CountingHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"value":"hello"}"""),
        });
        SourceContext context = Context(root, handler);
        Uri uri = new("https://api.github.com/test");

        Payload? first = await context.GetCachedJsonAsync<Payload>("key", uri, TimeSpan.FromHours(1));
        Payload? second = await context.GetCachedJsonAsync<Payload>("key", uri, TimeSpan.FromHours(1));

        Assert.Multiple(() =>
        {
            Assert.That(handler.Requests, Is.EqualTo(1), "fresh cache must not refetch");
            Assert.That(first!.Value, Is.EqualTo("hello"));
            Assert.That(second!.Value, Is.EqualTo("hello"));
        });
    }

    [Test]
    public async Task Expired_Cache_Revalidates_With_ETag_And_304_Reuses_Body()
    {
        using TempRoot root = new();
        CountingHandler handler = new(request =>
            request.Headers.Contains("If-None-Match")
                ? new HttpResponseMessage(HttpStatusCode.NotModified)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"value":"hello"}"""),
                    Headers = { ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"abc\"") },
                });
        SourceContext context = Context(root, handler);
        Uri uri = new("https://api.github.com/test");

        await context.GetCachedJsonAsync<Payload>("key", uri, TimeSpan.FromHours(1));
        Payload? revalidated = await context.GetCachedJsonAsync<Payload>("key", uri, TimeSpan.Zero);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Requests, Is.EqualTo(2));
            Assert.That(revalidated!.Value, Is.EqualTo("hello"));
        });
    }

    [Test]
    public async Task Offline_Uses_Stale_Cache_And_Throws_Without_One()
    {
        using TempRoot root = new();
        CountingHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"value":"hello"}"""),
        });
        Uri uri = new("https://api.github.com/test");

        await Context(root, handler).GetCachedJsonAsync<Payload>("key", uri, TimeSpan.FromHours(1));

        SourceContext offline = Context(root, handler, offline: true);
        Payload? cached = await offline.GetCachedJsonAsync<Payload>("key", uri, TimeSpan.Zero);
        Assert.That(cached!.Value, Is.EqualTo("hello"), "offline mode must serve stale cache");

        PythonException ex = Assert.ThrowsAsync<PythonException>(
            () => offline.GetCachedJsonAsync<Payload>("other-key", uri, TimeSpan.Zero))!;
        Assert.That(ex.Kind, Is.EqualTo(PythonErrorKind.Offline));
        Assert.That(handler.Requests, Is.EqualTo(1), "offline mode must never touch the network");
    }

    [Test]
    public async Task GitHub_Requests_Carry_Bearer_Token()
    {
        using TempRoot root = new();
        CountingHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"value":"hello"}"""),
        });
        SourceContext context = Context(root, handler, token: "tok123");

        await context.GetCachedJsonAsync<Payload>("key", new Uri("https://api.github.com/test"), TimeSpan.Zero);

        Assert.That(
            handler.LastRequest!.Headers.GetValues("Authorization").Single(),
            Is.EqualTo("Bearer tok123"));
    }

    [Test]
    public async Task Download_Caches_And_Verifies_Checksum()
    {
        using TempRoot root = new();
        byte[] payload = "archive-bytes"u8.ToArray();
        string sha256 = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(payload));
        CountingHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(payload),
        });
        SourceContext context = Context(root, handler);
        Uri uri = new("https://example.com/files/thing.tar.gz");

        string first = await context.DownloadAsync(uri, sha256);
        string second = await context.DownloadAsync(uri, sha256);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Requests, Is.EqualTo(1), "cached download must be reused");
            Assert.That(second, Is.EqualTo(first));
            Assert.That(File.ReadAllBytes(first), Is.EqualTo(payload));
        });

        PythonException ex = Assert.ThrowsAsync<PythonException>(
            () => context.DownloadAsync(new Uri("https://example.com/files/other.tar.gz"), new string('0', 64)))!;
        Assert.That(ex.Kind, Is.EqualTo(PythonErrorKind.DownloadFailed));
    }
}
