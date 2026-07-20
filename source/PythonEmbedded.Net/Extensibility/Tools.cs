using Microsoft.Extensions.Logging;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Internals;

namespace PythonEmbedded.Net.Extensibility;

/// <summary>
/// Resolves external tools (uv, poetry, micromamba, ...) strictly runtime-locally — system-installed
/// copies are deliberately never used, so deleting the runtime root deletes its tooling.
/// Resolution order: <c>PYEMBED_TOOL_&lt;NAME&gt;</c> environment variable (the only escape hatch) →
/// next to the installation's interpreter → <c>&lt;root&gt;/tools/</c> → the supplied provision callback.
/// </summary>
public static class Tools
{
    /// <summary>
    /// Returns the full path to <paramref name="name"/>, provisioning it via
    /// <paramref name="provision"/> (under a cross-process lock) when not yet present.
    /// When <paramref name="version"/> is given, the tool is resolved/cached under a version-specific
    /// directory so different requested versions of the same tool can coexist; the ambient
    /// next-to-the-interpreter location is only consulted when no specific version is requested.
    /// </summary>
    /// <exception cref="PythonException"><see cref="PythonErrorKind.ToolMissing"/> when provisioning ran but the tool still cannot be found.</exception>
    public static async Task<string> EnsureAsync(
        PythonInstallation install,
        string name,
        Func<ToolContext, CancellationToken, Task> provision,
        CancellationToken ct = default,
        string? version = null)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(provision);

        ToolContext context = install.Host.CreateToolContext(install);

        if (Resolve(install, name, context.ToolsDirectory, version) is { } existing)
        {
            return existing;
        }

        string lockKey = version is null ? name : $"{name}-{version}";
        string lockPath = Path.Combine(install.Host.LocksDirectory, $"tool-{lockKey.ToLowerInvariant()}.lock");
        using DiskLock _ = await DiskLock.AcquireAsync(lockPath, install.Host.Options.LockTimeout, ct).ConfigureAwait(false);

        // Another process may have provisioned it while we waited for the lock.
        if (Resolve(install, name, context.ToolsDirectory, version) is { } racedIn)
        {
            return racedIn;
        }

        context.Logger.LogInformation(
            "Provisioning tool '{Tool}'{Version}", name, version is null ? "" : $" version {version}");
        await provision(context, ct).ConfigureAwait(false);

        return Resolve(install, name, context.ToolsDirectory, version)
            ?? throw new PythonException(
                PythonErrorKind.ToolMissing,
                $"Tool '{name}' was provisioned but could not be found next to the interpreter or under '{context.ToolsDirectory}'. " +
                $"Set the PYEMBED_TOOL_{name.ToUpperInvariant()} environment variable to override resolution.");
    }

    /// <inheritdoc cref="EnsureAsync"/>
    public static string Ensure(
        PythonInstallation install, string name, Func<ToolContext, CancellationToken, Task> provision, string? version = null)
        => EnsureAsync(install, name, provision, version: version).GetAwaiter().GetResult();

    private static string? Resolve(PythonInstallation install, string name, string toolsDirectory, string? version)
    {
        string envVar = $"PYEMBED_TOOL_{name.ToUpperInvariant().Replace('-', '_')}";
        if (Environment.GetEnvironmentVariable(envVar) is { Length: > 0 } overridePath)
        {
            return File.Exists(overridePath)
                ? overridePath
                : throw new PythonException(
                    PythonErrorKind.ToolMissing, $"{envVar} points to '{overridePath}', which does not exist.");
        }

        string fileName = OperatingSystem.IsWindows() ? name + ".exe" : name;
        string scriptsSubdirectory = OperatingSystem.IsWindows() ? "Scripts" : "bin";
        string versionedDirectory = version is null ? name : $"{name}-{version}";
        List<string> candidates =
        [
            Path.Combine(toolsDirectory, versionedDirectory, fileName),
            // Version-pinned tools may be provisioned into a private venv; check its scripts dir too.
            Path.Combine(toolsDirectory, versionedDirectory, scriptsSubdirectory, fileName),
        ];
        if (version is null)
        {
            // Ambient locations only make sense when no specific version was requested — an ambient
            // binary's version is unknown, so it can't be trusted to satisfy a pinned request.
            string interpreterBin = Path.GetDirectoryName(install.PythonExecutable)!;
            candidates.Add(Path.Combine(interpreterBin, fileName));
            candidates.Add(Path.Combine(toolsDirectory, fileName));
        }

        return candidates.FirstOrDefault(File.Exists);
    }
}
