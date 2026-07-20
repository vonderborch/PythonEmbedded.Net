using System.Formats.Tar;
using System.IO.Compression;

namespace PythonEmbedded.Net.Test;

[TestFixture]
public class DirectorySourceTests
{
    private static void WriteArchive(string directory, string version, string tag, string triple)
    {
        string fileName = $"cpython-{version}+{tag}-{triple}-install_only.tar.gz";
        string exePath = OperatingSystem.IsWindows() ? "python/python.exe" : "python/bin/python3";

        Directory.CreateDirectory(directory);
        using FileStream file = File.Create(Path.Combine(directory, fileName));
        using GZipStream gzip = new(file, CompressionMode.Compress);
        using TarWriter tar = new(gzip);

        PaxTarEntry entry = new(TarEntryType.RegularFile, exePath);
        byte[] payload = "fake python"u8.ToArray();
        entry.DataStream = new MemoryStream(payload);
        if (!OperatingSystem.IsWindows())
        {
            entry.Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
        }

        tar.WriteEntry(entry);
    }

    private static async Task<PythonInstallation?> ResolveAsync(TempRoot root, string archiveDir, string request)
    {
        PythonHost host = new(root.Options(new DirectorySource("test-dir", archiveDir)));
        try
        {
            return await host.GetInstallationAsync(request, CancellationToken.None);
        }
        catch (PythonException ex) when (ex.Kind == PythonErrorKind.VersionNotFound)
        {
            return null;
        }
    }

    [Test]
    public async Task Extracts_Matching_Archive_And_Finds_Executable()
    {
        using TempRoot root = new();
        string archives = Path.Combine(root.Path, "archives");
        WriteArchive(archives, "3.13.5", "20260101", PlatformTriple.Current.Value);

        PythonInstallation? install = await ResolveAsync(root, archives, "3.13");

        Assert.That(install, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(install!.Version.ToString(), Is.EqualTo("3.13.5"));
            Assert.That(install.SourceName, Is.EqualTo("test-dir"));
            Assert.That(File.Exists(install.PythonExecutable));
        });
    }

    [Test]
    public async Task Ignores_Archives_For_Other_Platforms()
    {
        using TempRoot root = new();
        string archives = Path.Combine(root.Path, "archives");
        string otherTriple = PlatformTriple.Current.Value == "x86_64-pc-windows-msvc"
            ? "aarch64-apple-darwin"
            : "x86_64-pc-windows-msvc";
        WriteArchive(archives, "3.13.5", "20260101", otherTriple);

        Assert.That(await ResolveAsync(root, archives, "3.13"), Is.Null);
    }

    [Test]
    public async Task Picks_Highest_Version_Then_Newest_Tag()
    {
        using TempRoot root = new();
        string archives = Path.Combine(root.Path, "archives");
        string triple = PlatformTriple.Current.Value;
        WriteArchive(archives, "3.13.2", "20260101", triple);
        WriteArchive(archives, "3.13.9", "20260101", triple);
        WriteArchive(archives, "3.12.11", "20260401", triple);

        PythonInstallation? install = await ResolveAsync(root, archives, "3.13");

        Assert.That(install!.Version.ToString(), Is.EqualTo("3.13.9"));
    }

    [Test]
    public async Task Missing_Directory_Falls_Through()
    {
        using TempRoot root = new();
        Assert.That(await ResolveAsync(root, Path.Combine(root.Path, "does-not-exist"), "3.13"), Is.Null);
    }
}
