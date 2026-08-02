using System.Text.Json;
using System.Text.Json.Serialization;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Extensibility;

/// <summary>
/// Optional base for <see cref="IPackageInstaller"/> implementations. Hoists the patterns repeated
/// across the built-in and satellite installers (pip, uv, conda, poetry): run-a-subprocess-or-throw,
/// the standard <c>python -m venv</c> environment creation, plain pip install/uninstall/list, and
/// pinned-tool provisioning into a private venv. Implementing <see cref="IPackageInstaller"/> directly
/// remains supported for callers who want to implement multiple of these interfaces in one class.
/// </summary>
public abstract class PackageInstallerBase : IPackageInstaller
{
    /// <summary>Shared JSON options (web-casing) for parsing tool output such as <c>pip list --format=json</c>.</summary>
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract Task CreateEnvironmentAsync(PythonInstallation install, string envDirectory, CancellationToken ct);

    /// <inheritdoc />
    public abstract Task InstallAsync(PythonVirtualEnvironment env, PackageRequest request, CancellationToken ct);

    /// <inheritdoc />
    public abstract Task UninstallAsync(PythonVirtualEnvironment env, string package, CancellationToken ct);

    /// <inheritdoc />
    public abstract Task<IReadOnlyList<InstalledPackage>> ListAsync(PythonVirtualEnvironment env, CancellationToken ct);

    /// <inheritdoc />
    public abstract Task<bool> EnsureRequirementsAsync(PythonVirtualEnvironment env, string requirementsFile, CancellationToken ct);

    /// <inheritdoc />
    public abstract Task<IReadOnlyList<OutdatedPackage>> ListOutdatedAsync(PythonVirtualEnvironment env, CancellationToken ct);

    /// <summary>Runs a subprocess, throwing <see cref="PythonException"/> with <paramref name="kind"/> when it exits nonzero.</summary>
    protected static async Task<PythonResult> RunOrThrowAsync(
        string executable,
        IReadOnlyList<string> arguments,
        PythonErrorKind kind,
        string what,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string>? environment = null,
        CancellationToken ct = default)
    {
        PythonResult result = await Subprocess.RunAsync(executable, arguments, workingDirectory, environment, ct: ct)
            .ConfigureAwait(false);
        if (!result.Success)
        {
            throw new PythonException(kind, $"{what} failed (exit {result.ExitCode}): {result.StandardError.Trim()}");
        }

        return result;
    }

    /// <summary>The standard <c>python -m venv</c> environment creation, shared by installers that don't need a custom one.</summary>
    protected static Task CreateVenvAsync(PythonInstallation install, string envDirectory, CancellationToken ct)
        => RunOrThrowAsync(
            install.PythonExecutable, ["-m", "venv", envDirectory], PythonErrorKind.EnvironmentFailed, "venv creation", ct: ct);

    /// <summary>Plain <c>python -m pip install</c>, honoring <see cref="PackageRequest"/>'s index/requirements/extra args.</summary>
    protected static Task PipInstallAsync(PythonVirtualEnvironment env, PackageRequest request, CancellationToken ct)
    {
        List<string> args = ["-m", "pip", "install", "--disable-pip-version-check"];
        if (request.IndexUrl is not null)
        {
            args.AddRange(["--index-url", request.IndexUrl]);
        }

        if (request.RequirementsFile is not null)
        {
            args.AddRange(["-r", request.RequirementsFile]);
        }

        args.AddRange(request.ExtraArgs);
        args.AddRange(request.Packages);

        return RunOrThrowAsync(env.PythonExecutable, args, PythonErrorKind.PackageOperationFailed, "pip install", ct: ct);
    }

    /// <summary>Plain <c>python -m pip uninstall</c>.</summary>
    protected static Task PipUninstallAsync(PythonVirtualEnvironment env, string package, CancellationToken ct)
        => RunOrThrowAsync(
            env.PythonExecutable,
            ["-m", "pip", "uninstall", "--disable-pip-version-check", "-y", package],
            PythonErrorKind.PackageOperationFailed,
            $"pip uninstall of '{package}'",
            ct: ct);

    /// <summary>Plain <c>python -m pip list --format=json</c>, parsed into <see cref="InstalledPackage"/>s.</summary>
    protected static async Task<IReadOnlyList<InstalledPackage>> PipListAsync(PythonVirtualEnvironment env, CancellationToken ct)
    {
        PythonResult result = await RunOrThrowAsync(
            env.PythonExecutable,
            ["-m", "pip", "list", "--disable-pip-version-check", "--format=json"],
            PythonErrorKind.PackageOperationFailed,
            "pip list",
            ct: ct).ConfigureAwait(false);

        return JsonSerializer.Deserialize<List<InstalledPackage>>(result.StandardOutput, JsonOptions) ?? [];
    }

    /// <summary>
    /// Installs <paramref name="requirementsFile"/> via <c>pip install -r</c> (idempotent when everything is
    /// already satisfied) and reports whether the installed package set changed. This runs a real install
    /// rather than a true dry-run — pip has no reliable "would install nothing" signal short of diffing
    /// the package list before and after.
    /// </summary>
    protected static async Task<bool> PipEnsureRequirementsAsync(
        PythonVirtualEnvironment env, string requirementsFile, CancellationToken ct)
    {
        IReadOnlyList<InstalledPackage> before = await PipListAsync(env, ct).ConfigureAwait(false);
        await RunOrThrowAsync(
            env.PythonExecutable,
            ["-m", "pip", "install", "--disable-pip-version-check", "-r", requirementsFile],
            PythonErrorKind.PackageOperationFailed,
            "pip ensure requirements",
            ct: ct).ConfigureAwait(false);
        IReadOnlyList<InstalledPackage> after = await PipListAsync(env, ct).ConfigureAwait(false);
        return !before.SequenceEqual(after);
    }

    /// <summary>Plain <c>python -m pip list --outdated --format=json</c>, parsed into <see cref="OutdatedPackage"/>s.</summary>
    protected static async Task<IReadOnlyList<OutdatedPackage>> PipListOutdatedAsync(PythonVirtualEnvironment env, CancellationToken ct)
    {
        PythonResult result = await RunOrThrowAsync(
            env.PythonExecutable,
            ["-m", "pip", "list", "--outdated", "--disable-pip-version-check", "--format=json"],
            PythonErrorKind.PackageOperationFailed,
            "pip list --outdated",
            ct: ct).ConfigureAwait(false);
        return ParsePipStyleOutdatedJson(result.StandardOutput);
    }

    /// <summary>
    /// Parses pip-compatible <c>list --outdated --format=json</c> output (pip's keys are snake_case, unlike
    /// the web-casing <see cref="JsonOptions"/> covers implicitly for <see cref="InstalledPackage"/>). Shared
    /// by <see cref="PipListOutdatedAsync"/> and any pip-compatible tool (e.g. uv) parsing the same shape.
    /// </summary>
    protected static IReadOnlyList<OutdatedPackage> ParsePipStyleOutdatedJson(string json)
    {
        List<PipOutdatedDto> dtos = JsonSerializer.Deserialize<List<PipOutdatedDto>>(json, JsonOptions) ?? [];
        return dtos.Select(d => new OutdatedPackage(d.Name, d.Version, d.LatestVersion)).ToList();
    }

    private sealed record PipOutdatedDto(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("version")] string Version,
        [property: JsonPropertyName("latest_version")] string? LatestVersion);

    /// <summary>Full path to the interpreter inside a venv created at <paramref name="venvDirectory"/>.</summary>
    protected static string GetVenvPythonExecutable(string venvDirectory)
        => Path.Combine(
            venvDirectory,
            OperatingSystem.IsWindows() ? "Scripts" : "bin",
            OperatingSystem.IsWindows() ? "python.exe" : "python");

    /// <summary>
    /// pip-installs <paramref name="toolName"/> into the base interpreter — the "latest" (unpinned)
    /// tool-provisioning path shared by uv and poetry.
    /// </summary>
    protected static Task ProvisionToolViaPipAsync(ToolContext context, string toolName, CancellationToken ct)
        => RunOrThrowAsync(
            context.Installation.PythonExecutable,
            ["-m", "pip", "install", "--disable-pip-version-check", toolName],
            PythonErrorKind.ToolMissing,
            $"Provisioning {toolName} via pip",
            ct: ct);

    /// <summary>
    /// Creates a private venv at <c>&lt;toolsDirectory&gt;/&lt;toolName&gt;-&lt;version&gt;/</c> and pip-installs
    /// <c>toolName==version</c> into it — the pinned-version tool-provisioning path shared by uv and poetry,
    /// keyed by version so multiple pins (and the unpinned install) never clobber each other.
    /// </summary>
    protected static async Task ProvisionPinnedToolAsync(ToolContext context, string toolName, string version, CancellationToken ct)
    {
        string venvDirectory = Path.Combine(context.ToolsDirectory, $"{toolName}-{version}");
        await RunOrThrowAsync(
            context.Installation.PythonExecutable,
            ["-m", "venv", venvDirectory],
            PythonErrorKind.ToolMissing,
            $"Provisioning {toolName} {version}: venv creation",
            ct: ct).ConfigureAwait(false);

        await RunOrThrowAsync(
            GetVenvPythonExecutable(venvDirectory),
            ["-m", "pip", "install", "--disable-pip-version-check", $"{toolName}=={version}"],
            PythonErrorKind.ToolMissing,
            $"Provisioning {toolName} {version} via pip",
            ct: ct).ConfigureAwait(false);
    }
}
