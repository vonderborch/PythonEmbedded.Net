using System.Text.RegularExpressions;

namespace PythonEmbedded.Net.Internals;

/// <summary>
/// python-build-standalone's "install_only" archives bake the build machine's absolute install path
/// (<c>/install</c>) into <c>_sysconfigdata_*.py</c>. Left unpatched, <c>sysconfig.get_config_var(...)</c>
/// and anything that reads it (building C extensions from source, some build backends) resolves paths
/// that don't exist on the target machine. This rewrites those baked-in paths to the real install
/// location, mirroring what astral's own tooling does after extraction (see
/// https://gregoryszorc.com/docs/python-build-standalone/main/quirks.html). POSIX-only: Windows builds
/// don't carry this file. A no-op (and safe to call unconditionally) for installs that don't need it.
/// </summary>
internal static partial class SysconfigPatcher
{
    private const string OldPrefix = "/install";

    [GeneratedRegex(@"'((?:[^'\\]|\\.)*)'|""((?:[^""\\]|\\.)*)""")]
    private static partial Regex StringLiteral();

    public static void Patch(string installDirectory)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string? sysconfigFile = FindSysconfigDataFile(installDirectory);
        if (sysconfigFile is null)
        {
            return;
        }

        string text = File.ReadAllText(sysconfigFile);
        string patched = StringLiteral().Replace(text, match =>
        {
            string content = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            if (!content.Contains(OldPrefix, StringComparison.Ordinal))
            {
                return match.Value;
            }

            string quote = match.Value[0].ToString();
            string replaced = string.Join(
                ' ',
                content.Split(' ').Select(token => token.StartsWith(OldPrefix, StringComparison.Ordinal)
                    ? installDirectory + token[OldPrefix.Length..]
                    : token));

            return quote + replaced + quote;
        });

        if (patched != text)
        {
            File.WriteAllText(sysconfigFile, patched);
        }
    }

    private static string? FindSysconfigDataFile(string installDirectory)
    {
        // Archive layouts vary (some nest under a "python/" directory, some don't), so search
        // recursively rather than assuming a fixed depth.
        return Directory.EnumerateFiles(installDirectory, "_sysconfigdata_*.py", SearchOption.AllDirectories)
            .FirstOrDefault(f => (File.GetAttributes(f) & FileAttributes.ReparsePoint) == 0);
    }
}
