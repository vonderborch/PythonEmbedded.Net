using PythonEmbedded.Net;
using PythonEmbedded.Net.Extensibility;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Test;

/// <summary>A disposable temp directory acting as the runtime root for one test.</summary>
public sealed class TempRoot : IDisposable
{
    public TempRoot()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pyembed-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public PythonOptions Options(params IPythonSource[] sources)
    {
        PythonOptions options = new() { RootDirectory = Path, Offline = true };
        options.Sources.Clear();
        foreach (IPythonSource source in sources)
        {
            options.Sources.Add(source);
        }

        return options;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best effort.
        }
    }
}

/// <summary>A source that fabricates a minimal install tree with a dummy python executable.</summary>
public sealed class FakeSource : IPythonSource
{
    private readonly Models.PythonVersion? _version;

    public FakeSource(string? version = "3.13.5", string name = "fake")
    {
        Name = name;
        _version = version is null ? null : Models.PythonVersion.Parse(version);
    }

    public string Name { get; }

    public int InstallCount { get; private set; }

    public Task<PythonInstallInfo?> TryInstallAsync(
        PythonVersionRequest request, string targetDirectory, SourceContext context,
        IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        if (_version is null || !request.Matches(_version.Value))
        {
            return Task.FromResult<PythonInstallInfo?>(null);
        }

        InstallCount++;
        progress?.Report(new InstallProgress(InstallPhase.ResolvingMetadata));
        WriteFakePython(targetDirectory);
        progress?.Report(new InstallProgress(InstallPhase.Extracting));
        return Task.FromResult<PythonInstallInfo?>(new PythonInstallInfo(
            _version.Value, Name, context.Platform.Value, DateTimeOffset.UtcNow));
    }

    public static void WriteFakePython(string root)
    {
        string relative = OperatingSystem.IsWindows() ? "python/python.exe" : "python/bin/python3";
        string path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (OperatingSystem.IsWindows())
        {
            File.WriteAllText(path, "fake python");
            return;
        }

        // A trivial script rather than opaque bytes so diagnostics' "--version" smoke check can actually run it.
        File.WriteAllText(path, "#!/bin/sh\nexit 0\n");
        File.SetUnixFileMode(path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
    }
}

/// <summary>An installer that fabricates a venv-shaped directory without running anything.</summary>
public sealed class FakeInstaller : IPackageInstaller
{
    private readonly Dictionary<string, List<InstalledPackage>> _packagesByEnv = [];

    public FakeInstaller(string name = "fake")
    {
        Name = name;
    }

    public string Name { get; }

    public int CreateCount { get; private set; }

    public List<PackageRequest> Installed { get; } = [];

    public string? LastRequirementsFile { get; private set; }

    public bool EnsureRequirementsChanged { get; set; } = true;

    public List<OutdatedPackage> OutdatedToReturn { get; set; } = [];

    public Task CreateEnvironmentAsync(PythonInstallation install, string envDirectory, CancellationToken ct)
    {
        CreateCount++;
        string relative = OperatingSystem.IsWindows() ? "Scripts/python.exe" : "bin/python";
        string path = Path.Combine(envDirectory, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (OperatingSystem.IsWindows())
        {
            File.WriteAllText(path, "fake venv python");
        }
        else
        {
            // A trivial script rather than opaque bytes so diagnostics' "--version" smoke check can actually run it.
            File.WriteAllText(path, "#!/bin/sh\nexit 0\n");
            File.SetUnixFileMode(path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        return Task.CompletedTask;
    }

    public Task InstallAsync(PythonVirtualEnvironment env, PackageRequest request, CancellationToken ct)
    {
        Installed.Add(request);
        List<InstalledPackage> packages = GetPackages(env);
        foreach (string spec in request.Packages)
        {
            int split = spec.IndexOf("==", StringComparison.Ordinal);
            string name = split >= 0 ? spec[..split] : spec;
            string version = split >= 0 ? spec[(split + 2)..] : "0.0.0";
            packages.RemoveAll(p => p.Name == name);
            packages.Add(new InstalledPackage(name, version));
        }

        return Task.CompletedTask;
    }

    public Task UninstallAsync(PythonVirtualEnvironment env, string package, CancellationToken ct)
    {
        GetPackages(env).RemoveAll(p => p.Name == package);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<InstalledPackage>> ListAsync(PythonVirtualEnvironment env, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<InstalledPackage>>(GetPackages(env));

    public Task<bool> EnsureRequirementsAsync(PythonVirtualEnvironment env, string requirementsFile, CancellationToken ct)
    {
        LastRequirementsFile = requirementsFile;
        return Task.FromResult(EnsureRequirementsChanged);
    }

    public Task<IReadOnlyList<OutdatedPackage>> ListOutdatedAsync(PythonVirtualEnvironment env, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<OutdatedPackage>>(OutdatedToReturn);

    private List<InstalledPackage> GetPackages(PythonVirtualEnvironment env)
    {
        if (!_packagesByEnv.TryGetValue(env.Directory, out List<InstalledPackage>? packages))
        {
            packages = [];
            _packagesByEnv[env.Directory] = packages;
        }

        return packages;
    }
}

/// <summary>A runner that returns a canned result without launching a process.</summary>
public sealed class FakeRunner : IPythonRunner
{
    public FakeRunner(int exitCode = 0, string stdout = "", string stderr = "")
    {
        Result = new PythonResult(exitCode, stdout, stderr, TimeSpan.Zero);
    }

    public PythonResult Result { get; set; }

    public PythonInvocation? LastInvocation { get; private set; }

    public Task<PythonResult> RunAsync(PythonVirtualEnvironment env, PythonInvocation invocation, CancellationToken ct)
    {
        LastInvocation = invocation;
        return Task.FromResult(Result);
    }
}
