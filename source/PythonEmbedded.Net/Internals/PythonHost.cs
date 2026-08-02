using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Extensibility;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Internals;

/// <summary>
/// The internal engine behind the static <see cref="PythonEnvironment"/> facade: owns the on-disk layout,
/// resolves installations through the configured sources, and creates environments.
/// The filesystem is the index — a directory is valid only once its marker file
/// (<c>install.json</c> / <c>env.json</c>) exists, written last.
/// </summary>
internal sealed class PythonHost
{
    private const string InstallMarker = "install.json";
    private const string EnvMarker = "env.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly HttpClient SharedHttp = CreateSharedHttpClient();

    private readonly ILogger _logger;
    private readonly Lazy<bool> _initialized;

    public PythonHost(PythonOptions options)
    {
        Options = options;
        _logger = options.Logger ?? NullLogger.Instance;
        _initialized = new Lazy<bool>(Initialize);
    }

    public PythonOptions Options { get; }

    private string InstallsDirectory => Path.Combine(Options.RootDirectory, "installs");
    private string EnvsDirectory => Path.Combine(Options.RootDirectory, "envs");
    private string CacheDirectory => Path.Combine(Options.RootDirectory, "cache");
    private string TmpDirectory => Path.Combine(Options.RootDirectory, "tmp");

    public string LocksDirectory => Path.Combine(Options.RootDirectory, "locks");

    public string ToolsDirectory => Path.Combine(Options.RootDirectory, "tools");

    public ToolContext CreateToolContext(PythonInstallation install)
    {
        Directory.CreateDirectory(ToolsDirectory);
        return new ToolContext(install, ToolsDirectory, CreateSourceContext());
    }

    public async Task<PythonInstallation> GetInstallationAsync(
        string version, CancellationToken ct, IProgress<InstallProgress>? progress = null)
    {
        EnsureInitialized();
        PythonVersionRequest request = PythonVersionRequest.Parse(version);

        PythonInstallation? existing = FindInstallation(request);
        if (existing is not null)
        {
            return existing;
        }

        string lockPath = Path.Combine(LocksDirectory, $"install-{Sanitize(request.Raw)}.lock");
        progress?.Report(new InstallProgress(InstallPhase.WaitingForLock));
        using DiskLock _ = await DiskLock.AcquireAsync(lockPath, Options.LockTimeout, ct).ConfigureAwait(false);

        // Another process may have completed the install while we waited for the lock.
        existing = FindInstallation(request);
        if (existing is not null)
        {
            return existing;
        }

        SourceContext context = CreateSourceContext();
        foreach (IPythonSource source in Options.Sources)
        {
            ct.ThrowIfCancellationRequested();
            string staging = Path.Combine(TmpDirectory, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            try
            {
                PythonInstallInfo? info = await source.TryInstallAsync(request, staging, context, progress, ct).ConfigureAwait(false);
                if (info is null)
                {
                    TryDeleteDirectory(staging);
                    continue;
                }

                return CommitInstallation(info, staging, progress);
            }
            catch (Exception)
            {
                TryDeleteDirectory(staging);
                throw;
            }
        }

        throw new PythonException(
            PythonErrorKind.VersionNotFound,
            $"No configured source could provide Python '{request.Raw}' for {PlatformTriple.Current}. " +
            $"Sources tried: {string.Join(", ", Options.Sources.Select(s => s.Name))}.");
    }

    public async Task<PythonVirtualEnvironment> GetEnvironmentAsync(
        string version, string name, CancellationToken ct,
        IPackageInstaller? installer = null, IPythonRunner? runner = null, IProgress<InstallProgress>? progress = null)
    {
        PythonInstallation install = await GetInstallationAsync(version, ct, progress).ConfigureAwait(false);
        return await GetEnvironmentAsync(install, name, ct, installer, runner, progress).ConfigureAwait(false);
    }

    public async Task<PythonVirtualEnvironment> GetEnvironmentAsync(
        PythonInstallation install, string name, CancellationToken ct,
        IPackageInstaller? installer = null, IPythonRunner? runner = null, IProgress<InstallProgress>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not '.'))
        {
            throw new ArgumentException(
                $"Environment name '{name}' is invalid: use letters, digits, '-', '_', '.'.", nameof(name));
        }

        string envDirectory = Path.Combine(EnvsDirectory, install.InstallId, name);
        PythonVirtualEnvironment? existing = TryLoadEnvironment(install, name, envDirectory, installer, runner);
        if (existing is not null)
        {
            return existing;
        }

        string lockPath = Path.Combine(LocksDirectory, $"env-{install.InstallId}-{Sanitize(name)}.lock");
        progress?.Report(new InstallProgress(InstallPhase.WaitingForLock));
        using DiskLock _ = await DiskLock.AcquireAsync(lockPath, Options.LockTimeout, ct).ConfigureAwait(false);

        existing = TryLoadEnvironment(install, name, envDirectory, installer, runner);
        if (existing is not null)
        {
            return existing;
        }

        if (Directory.Exists(envDirectory))
        {
            // Marker missing: a previous creation attempt died partway. Start clean.
            TryDeleteDirectory(envDirectory);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(envDirectory)!);
        IPackageInstaller effectiveInstaller = installer ?? Options.Installer;
        IPythonRunner effectiveRunner = runner ?? Options.Runner;
        _logger.LogInformation("Creating environment '{Name}' for {InstallId} via {Installer}", name, install.InstallId, effectiveInstaller.Name);
        try
        {
            await effectiveInstaller.CreateEnvironmentAsync(install, envDirectory, ct).ConfigureAwait(false);
        }
        catch (Exception)
        {
            TryDeleteDirectory(envDirectory);
            throw;
        }

        string pythonExecutable = ProbeExecutable(envDirectory, VenvExecutableCandidates)
            ?? throw new PythonException(
                PythonErrorKind.EnvironmentFailed,
                $"Environment created at '{envDirectory}' but no python executable was found in it.");

        EnvMetadata metadata = new(name, install.InstallId, effectiveInstaller.Name, DateTimeOffset.UtcNow,
            Path.GetRelativePath(envDirectory, pythonExecutable));
        File.WriteAllText(Path.Combine(envDirectory, EnvMarker), JsonSerializer.Serialize(metadata, JsonOptions));

        return new PythonVirtualEnvironment(install, name, envDirectory, pythonExecutable, isBase: false, effectiveInstaller, effectiveRunner);
    }

    public Task<IReadOnlyList<PythonInstallation>> ListInstallationsAsync(CancellationToken ct)
    {
        EnsureInitialized();
        List<PythonInstallation> installations = [];
        foreach (string directory in EnumerateDirectoriesSafe(InstallsDirectory))
        {
            ct.ThrowIfCancellationRequested();
            PythonInstallation? install = TryLoadInstallation(directory);
            if (install is not null)
            {
                installations.Add(install);
            }
        }

        return Task.FromResult<IReadOnlyList<PythonInstallation>>(installations);
    }

    public async Task RemoveAsync(PythonInstallation install, CancellationToken ct)
    {
        EnsureInitialized();
        string lockPath = Path.Combine(LocksDirectory, $"install-remove-{install.InstallId}.lock");
        using DiskLock _ = await DiskLock.AcquireAsync(lockPath, Options.LockTimeout, ct).ConfigureAwait(false);

        _logger.LogInformation("Removing installation {InstallId} and its environments", install.InstallId);
        TryDeleteDirectory(Path.Combine(EnvsDirectory, install.InstallId), throwOnFailure: true);
        TryDeleteDirectory(install.Directory, throwOnFailure: true);
    }

    // ---- installation resolution ----

    private PythonInstallation CommitInstallation(PythonInstallInfo info, string staging, IProgress<InstallProgress>? progress = null)
    {
        string relativePython = info.RelativePythonPath
            ?? (ProbeExecutable(staging, InstallExecutableCandidates) is { } probed
                ? Path.GetRelativePath(staging, probed)
                : throw new PythonException(
                    PythonErrorKind.InstallFailed,
                    $"Source '{info.SourceName}' produced an install tree with no recognizable python executable."));

        string installId = $"cpython-{info.Version}-{Sanitize(info.SourceName)}";
        string finalDirectory = Path.Combine(InstallsDirectory, installId);

        if (Directory.Exists(finalDirectory))
        {
            // Left over from a crashed install (no marker — otherwise FindInstallation would have found it).
            TryDeleteDirectory(finalDirectory, throwOnFailure: true);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(finalDirectory)!);
        progress?.Report(new InstallProgress(InstallPhase.Committing));
        Directory.Move(staging, finalDirectory);
        progress?.Report(new InstallProgress(InstallPhase.Patching));
        SysconfigPatcher.Patch(finalDirectory);

        InstallMetadata metadata = new(
            info.Version.ToString(), info.SourceName, info.Triple, info.InstalledAt, info.Checksum, relativePython);
        File.WriteAllText(Path.Combine(finalDirectory, InstallMarker), JsonSerializer.Serialize(metadata, JsonOptions));

        _logger.LogInformation("Installed Python {Version} from '{Source}' at {Directory}", info.Version, info.SourceName, finalDirectory);
        return new PythonInstallation(
            info.Version, finalDirectory, Path.GetFullPath(Path.Combine(finalDirectory, relativePython)), info.SourceName, installId, this);
    }

    private PythonInstallation? FindInstallation(PythonVersionRequest request)
        => EnumerateDirectoriesSafe(InstallsDirectory)
            .Select(TryLoadInstallation)
            .Where(install => install is not null && request.Matches(install.Version))
            .OrderByDescending(install => install!.Version)
            .FirstOrDefault();

    private PythonInstallation? TryLoadInstallation(string directory)
    {
        InstallMetadata? metadata = TryReadJson<InstallMetadata>(Path.Combine(directory, InstallMarker));
        if (metadata is null || !Models.PythonVersion.TryParse(metadata.Version, out Models.PythonVersion version))
        {
            return null;
        }

        string executable = Path.GetFullPath(Path.Combine(directory, metadata.PythonExecutable));
        if (!File.Exists(executable))
        {
            return null;
        }

        return new PythonInstallation(
            version, directory, executable, metadata.SourceName, Path.GetFileName(directory), this);
    }

    private PythonVirtualEnvironment? TryLoadEnvironment(
        PythonInstallation install, string name, string envDirectory,
        IPackageInstaller? installer, IPythonRunner? runner)
    {
        EnvMetadata? metadata = TryReadJson<EnvMetadata>(Path.Combine(envDirectory, EnvMarker));
        if (metadata is null)
        {
            return null;
        }

        string executable = Path.GetFullPath(Path.Combine(envDirectory, metadata.PythonExecutable));
        if (!File.Exists(executable))
        {
            return null;
        }

        // The installer is fixed at creation time (it materially affects how the environment was built
        // on disk); the runner is a pure execution-time concern and is never recorded or validated.
        IPackageInstaller effectiveInstaller = installer ?? Options.Installer;
        if (!string.Equals(effectiveInstaller.Name, metadata.Installer, StringComparison.Ordinal))
        {
            throw new PythonException(
                PythonErrorKind.EnvironmentFailed,
                $"Environment '{name}' was created with installer '{metadata.Installer}' and cannot be reopened " +
                $"with '{effectiveInstaller.Name}'. An environment's installer is fixed for its lifetime.");
        }

        IPythonRunner effectiveRunner = runner ?? Options.Runner;
        return new PythonVirtualEnvironment(install, name, envDirectory, executable, isBase: false, effectiveInstaller, effectiveRunner);
    }

    // ---- diagnostics ----

    /// <summary>Whether <paramref name="installDirectory"/> has a readable, valid <c>install.json</c> marker.</summary>
    internal bool HasValidInstallMarker(string installDirectory)
        => TryReadJson<InstallMetadata>(Path.Combine(installDirectory, InstallMarker)) is not null;

    /// <summary>Whether <paramref name="envDirectory"/> has a readable, valid <c>env.json</c> marker.</summary>
    internal bool HasValidEnvMarker(string envDirectory)
        => TryReadJson<EnvMetadata>(Path.Combine(envDirectory, EnvMarker)) is not null;

    /// <summary>Lock files under <see cref="LocksDirectory"/> older than a day — likely abandoned by a crashed process.</summary>
    internal IReadOnlyList<string> InspectStaleLocks()
    {
        DateTime cutoff = DateTime.UtcNow.AddDays(-1);
        return Directory.Exists(LocksDirectory)
            ? Directory.EnumerateFiles(LocksDirectory)
                .Where(file => File.GetLastWriteTimeUtc(file) < cutoff)
                .ToList()
            : [];
    }

    // ---- plumbing ----

    private SourceContext CreateSourceContext() => new(
        Options.HttpClient ?? SharedHttp,
        Options.GitHubToken,
        CacheDirectory,
        PlatformTriple.Current,
        _logger,
        Options.Offline,
        Options.ReleaseCacheTtl);

    private void EnsureInitialized() => _ = _initialized.Value;

    private bool Initialize()
    {
        foreach (string directory in new[] { InstallsDirectory, EnvsDirectory, CacheDirectory, LocksDirectory, TmpDirectory })
        {
            Directory.CreateDirectory(directory);
        }

        CollectGarbage();
        return true;
    }

    /// <summary>Deletes abandoned staging directories and marker-less install directories older than a day.</summary>
    private void CollectGarbage()
    {
        DateTime cutoff = DateTime.UtcNow.AddDays(-1);
        foreach (string directory in EnumerateDirectoriesSafe(TmpDirectory))
        {
            if (Directory.GetCreationTimeUtc(directory) < cutoff)
            {
                TryDeleteDirectory(directory);
            }
        }

        foreach (string directory in EnumerateDirectoriesSafe(InstallsDirectory))
        {
            if (!File.Exists(Path.Combine(directory, InstallMarker)) && Directory.GetCreationTimeUtc(directory) < cutoff)
            {
                _logger.LogWarning("Removing incomplete installation directory {Directory}", directory);
                TryDeleteDirectory(directory);
            }
        }
    }

    private static readonly string[] InstallExecutableCandidates = OperatingSystem.IsWindows()
        ? ["python/python.exe", "python.exe", "python/Scripts/python.exe", "Scripts/python.exe"]
        : ["python/bin/python3", "python/bin/python", "bin/python3", "bin/python"];

    // Second Windows candidate covers conda-style environments (python.exe at the env root).
    private static readonly string[] VenvExecutableCandidates = OperatingSystem.IsWindows()
        ? ["Scripts/python.exe", "python.exe"]
        : ["bin/python"];

    private static string? ProbeExecutable(string root, string[] candidates)
        => candidates
            .Select(candidate => Path.GetFullPath(Path.Combine(root, candidate)))
            .FirstOrDefault(File.Exists);

    private static T? TryReadJson<T>(string path) where T : class
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private void TryDeleteDirectory(string directory, bool throwOnFailure = false)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException ex) when (!throwOnFailure)
        {
            _logger.LogDebug(ex, "Could not delete {Directory}", directory);
        }
        catch (UnauthorizedAccessException ex) when (!throwOnFailure)
        {
            _logger.LogDebug(ex, "Could not delete {Directory}", directory);
        }
    }

    private static IEnumerable<string> EnumerateDirectoriesSafe(string root)
        => Directory.Exists(root) ? Directory.EnumerateDirectories(root) : [];

    private static string Sanitize(string value)
        => string.Concat(value.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '-'));

    private static HttpClient CreateSharedHttpClient()
    {
        HttpClient client = new();
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "PythonEmbedded.Net");
        return client;
    }

    private sealed record InstallMetadata(
        string Version, string SourceName, string Triple, DateTimeOffset InstalledAt, string? Checksum, string PythonExecutable);

    private sealed record EnvMetadata(
        string Name, string InstallId, string Installer, DateTimeOffset CreatedAt, string PythonExecutable);
}
