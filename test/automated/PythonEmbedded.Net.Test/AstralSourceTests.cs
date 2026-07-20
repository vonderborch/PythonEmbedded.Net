using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;

namespace PythonEmbedded.Net.Test;

[TestFixture]
public class AstralSourceTests
{
    private const string Tag = "20260623";

    /// <summary>An in-memory GitHub: latest-release API, SHA256SUMS, and archive downloads.</summary>
    private sealed class FakeGitHub : HttpMessageHandler
    {
        private readonly Dictionary<string, byte[]> _archives = new();

        public int Requests { get; private set; }

        public void AddArchive(string version, string triple)
        {
            _archives[$"cpython-{version}+{Tag}-{triple}-install_only.tar.gz"] = BuildArchive();
        }

        public void AddBogusChecksumArchive(string version, string triple)
        {
            _archives[$"cpython-{version}+{Tag}-{triple}-install_only.tar.gz"] = BuildArchive();
            BogusChecksums = true;
        }

        private bool BogusChecksums { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests++;
            string path = request.RequestUri!.AbsolutePath;

            if (path.EndsWith("/releases/latest"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($$"""{"tag_name":"{{Tag}}"}"""),
                });
            }

            if (path.EndsWith("/SHA256SUMS"))
            {
                string sums = string.Join('\n', _archives.Select(kv =>
                    $"{(BogusChecksums ? new string('0', 64) : Convert.ToHexStringLower(SHA256.HashData(kv.Value)))}  {kv.Key}"));
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(sums) });
            }

            string fileName = Path.GetFileName(path);
            if (_archives.TryGetValue(fileName, out byte[]? bytes))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static byte[] BuildArchive()
        {
            using MemoryStream buffer = new();
            using (GZipStream gzip = new(buffer, CompressionMode.Compress, leaveOpen: true))
            using (TarWriter tar = new(gzip))
            {
                string exePath = OperatingSystem.IsWindows() ? "python/python.exe" : "python/bin/python3";
                PaxTarEntry entry = new(TarEntryType.RegularFile, exePath) { DataStream = new MemoryStream("fake python"u8.ToArray()) };
                if (!OperatingSystem.IsWindows())
                {
                    entry.Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
                }

                tar.WriteEntry(entry);
            }

            return buffer.ToArray();
        }
    }

    private static PythonHost Host(TempRoot root, FakeGitHub gitHub, bool offline = false)
    {
        PythonOptions options = new()
        {
            RootDirectory = root.Path,
            Offline = offline,
            HttpClient = new HttpClient(gitHub),
        };
        options.Sources.Clear();
        options.Sources.Add(new AstralSource());
        return new PythonHost(options);
    }

    [Test]
    public async Task Installs_From_GitHub_And_Caches_Everything()
    {
        using TempRoot root = new();
        FakeGitHub gitHub = new();
        gitHub.AddArchive("3.13.14", PlatformTriple.Current.Value);
        gitHub.AddArchive("3.12.13", PlatformTriple.Current.Value);
        PythonHost host = Host(root, gitHub);

        PythonInstallation install = await host.GetInstallationAsync("3.13", CancellationToken.None);
        int requestsAfterInstall = gitHub.Requests;
        PythonInstallation again = await host.GetInstallationAsync("3.13", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(install.Version.ToString(), Is.EqualTo("3.13.14"));
            Assert.That(install.SourceName, Is.EqualTo("astral"));
            Assert.That(File.Exists(install.PythonExecutable));
            Assert.That(requestsAfterInstall, Is.EqualTo(3), "latest + SHA256SUMS + archive");
            Assert.That(gitHub.Requests, Is.EqualTo(requestsAfterInstall), "second get must be fully local");
            Assert.That(again.Directory, Is.EqualTo(install.Directory));
        });
    }

    [Test]
    public void No_Matching_Asset_Falls_Through_To_VersionNotFound()
    {
        using TempRoot root = new();
        FakeGitHub gitHub = new();
        gitHub.AddArchive("3.13.14", "some-other-triple");
        PythonHost host = Host(root, gitHub);

        PythonException ex = Assert.ThrowsAsync<PythonException>(
            () => host.GetInstallationAsync("3.13", CancellationToken.None))!;
        Assert.That(ex.Kind, Is.EqualTo(PythonErrorKind.VersionNotFound));
    }

    [Test]
    public void Offline_Without_Cache_Falls_Through_Instead_Of_Throwing_Offline()
    {
        using TempRoot root = new();
        FakeGitHub gitHub = new();
        gitHub.AddArchive("3.13.14", PlatformTriple.Current.Value);
        PythonHost host = Host(root, gitHub, offline: true);

        PythonException ex = Assert.ThrowsAsync<PythonException>(
            () => host.GetInstallationAsync("3.13", CancellationToken.None))!;

        Assert.Multiple(() =>
        {
            Assert.That(ex.Kind, Is.EqualTo(PythonErrorKind.VersionNotFound));
            Assert.That(gitHub.Requests, Is.Zero, "offline mode must never touch the network");
        });
    }

    [Test]
    public async Task Offline_With_Cached_Metadata_And_Archive_Installs()
    {
        using TempRoot root = new();
        FakeGitHub gitHub = new();
        gitHub.AddArchive("3.13.14", PlatformTriple.Current.Value);

        PythonInstallation online = await Host(root, gitHub).GetInstallationAsync("3.13", CancellationToken.None);
        Directory.Delete(online.Directory, recursive: true); // drop the install, keep the caches

        int requests = gitHub.Requests;
        PythonInstallation offline = await Host(root, gitHub, offline: true).GetInstallationAsync("3.13", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(offline.Version.ToString(), Is.EqualTo("3.13.14"));
            Assert.That(gitHub.Requests, Is.EqualTo(requests), "offline reinstall must be served from cache");
        });
    }

    [Test]
    public void Checksum_Mismatch_Fails_The_Download()
    {
        using TempRoot root = new();
        FakeGitHub gitHub = new();
        gitHub.AddBogusChecksumArchive("3.13.14", PlatformTriple.Current.Value);
        PythonHost host = Host(root, gitHub);

        PythonException ex = Assert.ThrowsAsync<PythonException>(
            () => host.GetInstallationAsync("3.13", CancellationToken.None))!;
        Assert.That(ex.Kind, Is.EqualTo(PythonErrorKind.DownloadFailed));
    }

    [Test]
    public void ArchiveName_Parses_And_Rejects()
    {
        Assert.Multiple(() =>
        {
            ArchiveName? parsed = ArchiveName.TryParse("cpython-3.13.14+20260623-aarch64-apple-darwin-install_only.tar.gz");
            Assert.That(parsed, Is.Not.Null);
            Assert.That(parsed!.Version.ToString(), Is.EqualTo("3.13.14"));
            Assert.That(parsed.Tag, Is.EqualTo("20260623"));
            Assert.That(parsed.Triple, Is.EqualTo("aarch64-apple-darwin"));

            Assert.That(ArchiveName.TryParse("cpython-3.13.14+20260623-aarch64-apple-darwin-full.tar.zst"), Is.Null);
            Assert.That(ArchiveName.TryParse("not-an-archive.tar.gz"), Is.Null);
        });
    }
}
