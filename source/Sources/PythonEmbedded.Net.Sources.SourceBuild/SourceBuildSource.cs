using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Extensibility;
using PythonEmbedded.Net.Models;
using PythonEmbedded.Net.Sources.SourceBuild.Internals;

namespace PythonEmbedded.Net.Sources.SourceBuild;

/// <summary>
/// An <see cref="IPythonSource"/> that downloads official CPython source tarballs from python.org and
/// compiles them locally. Use it for what prebuilt archives cannot give you: variants nobody publishes
/// (free-threaded, <c>--with-pydebug</c>), a build linked against this machine's own OpenSSL, or a
/// platform with no prebuilt archive at all.
/// <para>
/// Register it like any other satellite — first, so it wins over the built-in sources:
/// <code>
/// PythonEnvironment.Configure(o => o.Sources.Insert(0, new SourceBuildSource()));
/// </code>
/// </para>
/// <para>
/// <b>The first call is slow.</b> A default (PGO + LTO) build takes roughly 15–40 minutes on a typical
/// machine. The result is cached by the host like any other installation, so only the first
/// <c>GetEnvironmentAsync</c> for a given version pays it — but pass an
/// <see cref="IProgress{T}"/> of <see cref="InstallProgress"/> and show it, or your users will assume
/// the process has hung. Set <see cref="Optimize"/> to false for a build that takes minutes instead.
/// </para>
/// <para>
/// <b>Trust model.</b> python.org publishes no checksum manifest, so the tarball is fetched over HTTPS
/// and its SHA-256 is recorded in the installation's <c>install.json</c> as provenance. Set
/// <see cref="ExpectedSha256"/> to pin a specific tarball and have the download verified against it.
/// </para>
/// </summary>
/// <remarks>
/// This implements <see cref="IPythonSource"/> directly rather than deriving from
/// <see cref="PythonSourceBase"/> so that <see cref="Name"/> can be set at construction — installs are
/// keyed by <c>cpython-{version}-{sourceName}</c>, so build variants must be named apart.
/// </remarks>
public sealed class SourceBuildSource : IPythonSource
{
    private readonly string? name;

    /// <summary>
    /// Identifies this source in install directory names (<c>installs/cpython-3.13.7-source-build/</c>)
    /// and diagnostics. Defaults to <c>"source-build"</c>, or <c>"source-build-ft"</c> when
    /// <see cref="FreeThreaded"/> is set.
    /// <para>
    /// <b>Set this whenever you register more than one differently-configured instance.</b> Two builds
    /// that share a name and version share an install directory, so the first one built wins and the
    /// second is never compiled — a debug build and a release build of 3.13.7 must not both be called
    /// <c>"source-build"</c>.
    /// </para>
    /// </summary>
    public string Name
    {
        get => this.name ?? (FreeThreaded ? "source-build-ft" : "source-build");
        init => this.name = value;
    }

    /// <summary>
    /// Builds with <c>--enable-optimizations</c> (PGO). Default true. This is where nearly all of the
    /// build time goes — the interpreter is compiled, exercised against a training workload, then
    /// recompiled — in exchange for roughly 10–20% faster Python code.
    /// </summary>
    public bool Optimize { get; init; } = true;

    /// <summary>Builds with <c>--with-lto</c> (link-time optimization). Default true.</summary>
    public bool Lto { get; init; } = true;

    /// <summary>
    /// Builds a shared <c>libpython</c> (<c>--enable-shared</c>). Default true, and required by
    /// in-process runners such as the Python.NET satellite, which need a library to load rather than an
    /// executable to spawn.
    /// </summary>
    public bool Shared { get; init; } = true;

    /// <summary>
    /// Builds the free-threaded (no-GIL) interpreter, <c>--disable-gil</c>. Requires CPython 3.13 or
    /// newer. The default <see cref="Name"/> becomes <c>"source-build-ft"</c> so these installs never
    /// collide with regular ones.
    /// </summary>
    public bool FreeThreaded { get; init; }

    /// <summary>Parallelism for <c>make</c>. Defaults to <see cref="Environment.ProcessorCount"/>.</summary>
    public int JobCount { get; init; } = Environment.ProcessorCount;

    /// <summary>
    /// Installs missing build dependencies instead of failing. Default false: by default this source
    /// detects what is missing and throws with the exact command that would install it, because
    /// installing system-wide software is not something a library should do behind your back.
    /// <para>
    /// When enabled, the root-free route is always tried first — Homebrew on macOS, a conda-forge prefix
    /// under <c>&lt;root&gt;/tools/</c> on Linux. Anything needing administrator rights additionally
    /// requires <see cref="AllowElevation"/>.
    /// </para>
    /// </summary>
    public bool ProvisionDependencies { get; init; }

    /// <summary>
    /// Permits provisioning steps that need administrator rights: <c>sudo</c> on Linux (with <c>-n</c>,
    /// so a password prompt fails fast rather than hanging), and the UAC prompt winget raises when
    /// installing the Visual Studio build tools on Windows. Default false, and meaningless on its own —
    /// <see cref="ProvisionDependencies"/> must also be set.
    /// </summary>
    public bool AllowElevation { get; init; }

    /// <summary>Extra arguments appended to <c>configure</c> (or <c>PCbuild\build.bat</c> on Windows).</summary>
    public IReadOnlyList<string> ConfigureArguments { get; init; } = [];

    /// <summary>
    /// Environment variables for the build. Applied last, so they override everything this source
    /// composes — including <c>CC</c>, <c>CPPFLAGS</c> and <c>LDFLAGS</c>.
    /// </summary>
    public IReadOnlyDictionary<string, string> BuildEnvironment { get; init; } = new Dictionary<string, string>();

    /// <summary>Per-command timeout for every build step. Default two hours.</summary>
    public TimeSpan BuildTimeout { get; init; } = TimeSpan.FromHours(2);

    /// <summary>
    /// Keeps the unpacked source and object files under <c>&lt;root&gt;/cache/source-build/</c> after a
    /// successful build. They are deleted by default; keeping them makes a rebuild much faster and is
    /// useful when iterating on <see cref="ConfigureArguments"/>.
    /// </summary>
    public bool KeepBuildDirectory { get; init; }

    /// <summary>
    /// SHA-256 the downloaded tarball must match, as hex. Null (the default) accepts whatever python.org
    /// serves over HTTPS and records its hash rather than checking it.
    /// </summary>
    public string? ExpectedSha256 { get; init; }

    /// <inheritdoc />
    public async Task<PythonInstallInfo?> TryInstallAsync(
        PythonVersionRequest request, string targetDirectory, SourceContext context,
        IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        PythonOrgCatalog.Release? release;
        try
        {
            progress?.Report(new InstallProgress(InstallPhase.ResolvingMetadata, Detail: Name));
            release = await PythonOrgCatalog
                .ResolveAsync(request, context, context.ReleaseCacheTtl, ct).ConfigureAwait(false);
        }
        catch (PythonException ex) when (ex.Kind == PythonErrorKind.Offline)
        {
            // Nothing cached and no network: stay quiet so the next source gets its turn.
            context.Logger.LogDebug("source-build is unavailable offline without cached python.org metadata");
            return null;
        }

        if (release is null)
        {
            context.Logger.LogDebug("python.org has no source release matching '{Request}'", request.Raw);
            return null;
        }

        if (FreeThreaded && (release.Version.Major < 3 || (release.Version.Major == 3 && release.Version.Minor < 13)))
        {
            throw new PythonException(
                PythonErrorKind.UnsupportedPlatform,
                $"Free-threaded builds need CPython 3.13 or newer; '{request.Raw}' resolved to {release.Version}.");
        }

        string tarball = await context.DownloadAsync(release.TarballUri, ExpectedSha256, ct, progress).ConfigureAwait(false);
        string checksum = await ComputeSha256Async(tarball, ct).ConfigureAwait(false);

        BuildOptions options = new(
            Name, Optimize, Lto, Shared, FreeThreaded, JobCount, ProvisionDependencies, AllowElevation,
            ConfigureArguments, BuildEnvironment, BuildTimeout, KeepBuildDirectory);

        BuildLog log = new(context.CacheDirectory, release.Version, context.Logger, progress, BuildTimeout);
        context.Logger.LogInformation(
            "Compiling CPython {Version} from source{Profile}. This takes a while; progress is reported per build step and the full log is at {Log}",
            release.Version, Optimize ? " with optimizations (15-40 minutes is normal)" : string.Empty, log.FilePath);

        string workRoot = Path.Combine(context.CacheDirectory, "source-build");
        BuildPaths paths = new(
            release.Version,
            Path.Combine(workRoot, "src", $"Python-{release.Version}"),
            Path.Combine(workRoot, $"build-{release.Version}-{options.Fingerprint}"),
            targetDirectory);

        await ExtractSourceAsync(tarball, workRoot, paths, context, progress, ct).ConfigureAwait(false);

        if (OperatingSystem.IsWindows())
        {
            await WindowsToolchain.EnsureAsync(options, log, ct).ConfigureAwait(false);
            await WindowsBuilder.BuildAsync(options, paths, log, ct).ConfigureAwait(false);
        }
        else if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
        {
            ToolchainEnvironment toolchain = await PosixToolchain
                .PrepareAsync(options, release.Version, context, log, ct).ConfigureAwait(false);
            await PosixBuilder.BuildAsync(options, paths, toolchain, log, ct).ConfigureAwait(false);
        }
        else
        {
            throw new PythonException(
                PythonErrorKind.UnsupportedPlatform,
                "Building CPython from source is supported on Windows, macOS and Linux only.");
        }

        string relativePython = OperatingSystem.IsWindows()
            ? "python.exe"
            : Path.Combine("bin", "python3");
        await ModuleVerifier
            .VerifyAsync(Path.Combine(targetDirectory, relativePython), log, progress, ct).ConfigureAwait(false);

        CleanUp(paths, context);

        return new PythonInstallInfo(
            release.Version, Name, context.Platform.Value, DateTimeOffset.UtcNow, checksum, relativePython);
    }

    /// <summary>
    /// Unpacks the tarball, which contains its own <c>Python-X.Y.Z/</c> root. A previous tree is replaced
    /// rather than reused: an interrupted extraction leaves a directory that looks complete but is not.
    /// </summary>
    private static async Task ExtractSourceAsync(
        string tarball, string workRoot, BuildPaths paths, SourceContext context,
        IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        if (Directory.Exists(paths.SourceDirectory))
        {
            Directory.Delete(paths.SourceDirectory, recursive: true);
        }

        await context.ExtractAsync(tarball, Path.Combine(workRoot, "src"), ct, progress).ConfigureAwait(false);

        if (!Directory.Exists(paths.SourceDirectory))
        {
            throw new PythonException(
                PythonErrorKind.InstallFailed,
                $"'{Path.GetFileName(tarball)}' did not contain the expected '{Path.GetFileName(paths.SourceDirectory)}' directory.");
        }
    }

    private void CleanUp(BuildPaths paths, SourceContext context)
    {
        if (KeepBuildDirectory)
        {
            context.Logger.LogInformation(
                "Keeping build artifacts at {Source} and {Build}", paths.SourceDirectory, paths.BuildDirectory);
            return;
        }

        foreach (string directory in new[] { paths.BuildDirectory, paths.SourceDirectory })
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch (IOException ex)
            {
                // Disk space, not correctness: the interpreter is already built and verified.
                context.Logger.LogDebug(ex, "Could not delete build directory {Directory}", directory);
            }
            catch (UnauthorizedAccessException ex)
            {
                context.Logger.LogDebug(ex, "Could not delete build directory {Directory}", directory);
            }
        }
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
    {
        await using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false));
    }
}
