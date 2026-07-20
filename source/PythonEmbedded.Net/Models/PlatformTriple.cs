using System.Runtime.InteropServices;

namespace PythonEmbedded.Net;

/// <summary>
/// The platform identifier used by python-build-standalone asset names,
/// e.g. <c>aarch64-apple-darwin</c> or <c>x86_64-pc-windows-msvc</c>.
/// </summary>
public readonly record struct PlatformTriple(string Value)
{
    private static readonly Lazy<PlatformTriple> CurrentLazy = new(Detect);

    /// <summary>The triple for the machine this process is running on.</summary>
    public static PlatformTriple Current => CurrentLazy.Value;

    private static PlatformTriple Detect()
    {
        string arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x86_64",
            Architecture.Arm64 => "aarch64",
            var other => throw new PythonException(
                PythonErrorKind.UnsupportedPlatform,
                $"Unsupported CPU architecture '{other}'. Supported: x64, arm64."),
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new PlatformTriple($"{arch}-pc-windows-msvc");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new PlatformTriple($"{arch}-apple-darwin");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new PlatformTriple($"{arch}-unknown-linux-gnu");
        }

        throw new PythonException(
            PythonErrorKind.UnsupportedPlatform,
            $"Unsupported operating system '{RuntimeInformation.OSDescription}'. Supported: Windows, macOS, Linux.");
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
