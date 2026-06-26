using System.Runtime.InteropServices;

namespace PythonEmbedded.Net.Models;

/// <summary>
/// Represents platform information (operating system and architecture).
/// </summary>
public class PlatformInfo
{
    /// <summary>
    /// Gets or sets the operating system (e.g., "Windows", "Linux", "macOS").
    /// </summary>
    public string OperatingSystem { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the architecture (e.g., "x64", "x86", "ARM64", "ARMv7").
    /// </summary>
    public string Architecture { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the python-build-standalone target triple (e.g., "x86_64-pc-windows-msvc").
    /// </summary>
    public string? TargetTriple { get; set; }

    /// <summary>
    /// Detects the current platform and returns platform information.
    /// </summary>
    /// <returns>A <see cref="PlatformInfo"/> object containing platform information.</returns>
    /// <exception cref="PlatformNotSupportedException">Thrown when the platform is not supported.</exception>
    public static PlatformInfo Detect()
    {
        var os = GetOperatingSystem();
        var architecture = GetArchitecture();
        var targetTriple = GetTargetTriple(os, architecture);

        return new PlatformInfo
        {
            OperatingSystem = os,
            Architecture = architecture,
            TargetTriple = targetTriple
        };
    }

    private static string GetOperatingSystem()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macOS"
            : RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD) ? "FreeBSD"
            : throw new Exceptions.PlatformNotSupportedException(
                $"Unsupported operating system: {RuntimeInformation.OSDescription}");
    }

    private static string GetArchitecture()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.X64 => "x64",
            System.Runtime.InteropServices.Architecture.X86 => "x86",
            System.Runtime.InteropServices.Architecture.Arm64 => "ARM64",
            System.Runtime.InteropServices.Architecture.Arm => "ARMv7",
            _ => throw new Exceptions.PlatformNotSupportedException(
                $"Unsupported architecture: {RuntimeInformation.ProcessArchitecture}")
        };
    }

    private static string GetTargetTriple(string os, string architecture)
    {
        return (os, architecture) switch
        {
            // Windows
            ("Windows", "x64") => "x86_64-pc-windows-msvc",
            ("Windows", "x86") => "i686-pc-windows-msvc",

            // Linux
            ("Linux", "x64") => DetectLinuxLibc() == "musl"
                ? "x86_64-unknown-linux-musl"
                : "x86_64-unknown-linux-gnu",
            ("Linux", "x86") => "i686-unknown-linux-gnu",
            ("Linux", "ARM64") => "aarch64-unknown-linux-gnu",
            ("Linux", "ARMv7") => "armv7-unknown-linux-gnueabi",

            // macOS
            ("macOS", "x64") => "x86_64-apple-darwin",
            ("macOS", "ARM64") => "aarch64-apple-darwin",

            _ => throw new Exceptions.PlatformNotSupportedException(
                $"Unsupported platform combination: {os} {architecture}")
        };
    }

    private static string DetectLinuxLibc()
    {
        try
        {
            var lddPath = "/usr/bin/ldd";
            if (File.Exists(lddPath))
            {
                return "glibc";
            }
        }
        catch
        {
            // If we can't detect, default to glibc
        }

        return "glibc";
    }

    /// <summary>
    /// Validates that the minimum OS version requirements are met for this platform.
    /// Based on python-build-standalone requirements:
    /// - Windows: Requires Windows 8 or Windows Server 2012 or newer
    /// - Linux: Requires glibc 2.17 or newer
    /// - macOS: No specific version check (Python will fail if incompatible)
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">Thrown when the OS version is too old.</exception>
    public void ValidateMinimumOsVersion()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ValidateWindowsVersion();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            ValidateLinuxGlibcVersion();
        }
        // macOS: No specific version check needed - Python will fail gracefully if incompatible
    }

    private static void ValidateWindowsVersion()
    {
        // Windows 8 = Version 6.2 (Build 9200)
        // Windows Server 2012 = Version 6.2 (Build 9200)
        // Check that we're on Windows 8 (6.2) or newer
        var osVersion = Environment.OSVersion;
        if (osVersion.Version.Major < 6 || (osVersion.Version.Major == 6 && osVersion.Version.Minor < 2))
        {
            throw new Exceptions.PlatformNotSupportedException(
                "Python standalone builds require Windows 8 or Windows Server 2012 or newer. " +
                $"Current OS version: {osVersion.VersionString}")
            {
                Platform = "Windows"
            };
        }

        // Check for vcruntime140.dll (Microsoft Visual C++ Redistributable)
        // This is not included in the Python distribution and must be present on the system
        var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var vcruntimePath = Path.Combine(system32, "vcruntime140.dll");
        if (!File.Exists(vcruntimePath))
        {
            // Don't throw, but log a warning - Python may still work if the DLL is in PATH
            // The actual execution will fail if it's missing, so we'll catch it there
            System.Diagnostics.Debug.WriteLine(
                "Warning: vcruntime140.dll not found in System32. " +
                "Microsoft Visual C++ Redistributable may be required for Python to run.");
        }
    }

    private static void ValidateLinuxGlibcVersion()
    {
        // glibc 2.17 is the minimum required version
        // We'll try to read the version from the system
        try
        {
            // Try to get glibc version using ldd (common on Linux systems)
            var lddPath = "/usr/bin/ldd";
            if (File.Exists(lddPath))
            {
                // ldd --version outputs something like:
                // ldd (Debian GLIBC 2.31-13+deb11u7) 2.31
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = lddPath,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,  // Prevent stdin blocking on TTY
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processInfo);
                if (process != null)
                {
                    // Close stdin immediately to prevent blocking
                    process.StandardInput.Close();
                    
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    // Extract version number from output (e.g., "2.31" from "ldd (Debian GLIBC 2.31-13+deb11u7) 2.31")
                    var versionMatch = System.Text.RegularExpressions.Regex.Match(output, @"(\d+)\.(\d+)");
                    if (versionMatch.Success)
                    {
                        int major = int.Parse(versionMatch.Groups[1].Value);
                        int minor = int.Parse(versionMatch.Groups[2].Value);

                        // Check if version is >= 2.17
                        if (major < 2 || (major == 2 && minor < 17))
                        {
                            throw new Exceptions.PlatformNotSupportedException(
                                $"Python standalone builds require glibc 2.17 or newer. " +
                                $"Detected glibc version: {major}.{minor}")
                            {
                                Platform = "Linux"
                            };
                        }

                        return; // Validation passed
                    }
                }
            }
        }
        catch (Exceptions.PlatformNotSupportedException)
        {
            throw; // Re-throw platform not supported exceptions
        }
        catch
        {
            // If we can't determine glibc version, we'll let Python execution fail naturally
            // This is better than blocking users on systems where our detection fails
            System.Diagnostics.Debug.WriteLine(
                "Warning: Could not determine glibc version. " +
                "Python execution will fail if glibc 2.17+ is not available.");
        }
    }
}

