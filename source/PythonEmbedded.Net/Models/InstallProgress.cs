namespace PythonEmbedded.Net.Models;

/// <summary>Stage of interpreter acquisition being reported by an <see cref="InstallProgress"/> update.</summary>
public enum InstallPhase
{
    /// <summary>Waiting to acquire the cross-process install lock.</summary>
    WaitingForLock,

    /// <summary>Resolving release/version metadata over the network.</summary>
    ResolvingMetadata,

    /// <summary>Downloading the interpreter archive.</summary>
    Downloading,

    /// <summary>Extracting the archive into the install directory.</summary>
    Extracting,

    /// <summary>Patching baked-in sysconfig paths.</summary>
    Patching,

    /// <summary>Moving the staged install into place and writing its marker.</summary>
    Committing,
}

/// <summary>A single progress update reported during <see cref="PythonEnvironment.GetInstallationAsync"/>.</summary>
/// <param name="Phase">The stage currently in progress.</param>
/// <param name="BytesCompleted">Bytes transferred so far, when known (typically only during <see cref="InstallPhase.Downloading"/>).</param>
/// <param name="BytesTotal">Total expected bytes, when known.</param>
/// <param name="Detail">Optional free-form detail, e.g. a file or release name.</param>
public sealed record InstallProgress(
    InstallPhase Phase,
    long? BytesCompleted = null,
    long? BytesTotal = null,
    string? Detail = null);
