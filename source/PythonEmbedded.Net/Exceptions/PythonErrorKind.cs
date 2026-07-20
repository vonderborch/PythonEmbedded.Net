namespace PythonEmbedded.Net.Exceptions;

/// <summary>Categorizes a <see cref="PythonException"/> for programmatic handling.</summary>
public enum PythonErrorKind
{
    /// <summary>No configured source could satisfy the requested Python version.</summary>
    VersionNotFound,

    /// <summary>The current OS/architecture is not supported by any known Python build.</summary>
    UnsupportedPlatform,

    /// <summary>A download failed or its checksum did not match.</summary>
    DownloadFailed,

    /// <summary>Materializing a Python installation on disk failed.</summary>
    InstallFailed,

    /// <summary>Creating or preparing a virtual environment failed.</summary>
    EnvironmentFailed,

    /// <summary>Installing, uninstalling, or listing packages failed.</summary>
    PackageOperationFailed,

    /// <summary>A required external tool is missing and could not be provisioned.</summary>
    ToolMissing,

    /// <summary>A cross-process lock could not be acquired within the timeout.</summary>
    Locked,

    /// <summary>The operation required the network while <see cref="PythonOptions.Offline"/> is set (or no cached data exists).</summary>
    Offline,

    /// <summary>Running Python code failed (see <see cref="PythonProcessException"/>).</summary>
    ExecutionFailed,

    /// <summary>A Python process exceeded its configured timeout and was killed.</summary>
    Timeout,
}
