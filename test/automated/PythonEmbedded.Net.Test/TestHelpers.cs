using PythonEmbedded.Net;

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
    private readonly PythonVersion? _version;

    public FakeSource(string? version = "3.13.5", string name = "fake")
    {
        Name = name;
        _version = version is null ? null : PythonVersion.Parse(version);
    }

    public string Name { get; }

    public int InstallCount { get; private set; }

    public Task<PythonInstallInfo?> TryInstallAsync(
        PythonVersionRequest request, string targetDirectory, SourceContext context, CancellationToken ct)
    {
        if (_version is null || !request.Matches(_version.Value))
        {
            return Task.FromResult<PythonInstallInfo?>(null);
        }

        InstallCount++;
        WriteFakePython(targetDirectory);
        return Task.FromResult<PythonInstallInfo?>(new PythonInstallInfo(
            _version.Value, Name, context.Platform.Value, DateTimeOffset.UtcNow));
    }

    public static void WriteFakePython(string root)
    {
        string relative = OperatingSystem.IsWindows() ? "python/python.exe" : "python/bin/python3";
        string path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "fake python");
    }
}

/// <summary>An installer that fabricates a venv-shaped directory without running anything.</summary>
public sealed class FakeInstaller : IPackageInstaller
{
    public string Name => "fake";

    public int CreateCount { get; private set; }

    public List<PackageRequest> Installed { get; } = [];

    public Task CreateEnvironmentAsync(PythonInstallation install, string envDirectory, CancellationToken ct)
    {
        CreateCount++;
        string relative = OperatingSystem.IsWindows() ? "Scripts/python.exe" : "bin/python";
        string path = Path.Combine(envDirectory, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "fake venv python");
        return Task.CompletedTask;
    }

    public Task InstallAsync(PythonVirtualEnvironment env, PackageRequest request, CancellationToken ct)
    {
        Installed.Add(request);
        return Task.CompletedTask;
    }

    public Task UninstallAsync(PythonVirtualEnvironment env, string package, CancellationToken ct) => Task.CompletedTask;

    public Task<IReadOnlyList<InstalledPackage>> ListAsync(PythonVirtualEnvironment env, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<InstalledPackage>>([]);
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
