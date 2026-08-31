namespace PythonEmbedded.Net.Sources.SourceBuild.Internals;

/// <summary>PATH lookup, so toolchain probing never shells out to <c>which</c>/<c>where</c>.</summary>
internal static class Executables
{
    /// <summary>The full path of <paramref name="name"/> on PATH, or null when it isn't there.</summary>
    public static string? Which(string name)
    {
        string[] extensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT").Split(';', StringSplitOptions.RemoveEmptyEntries)
            : [string.Empty];

        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (string extension in extensions)
            {
                string candidate;
                try
                {
                    candidate = Path.Combine(directory.Trim(), name + extension);
                }
                catch (ArgumentException)
                {
                    // A malformed PATH entry; skip it rather than failing the whole probe.
                    continue;
                }

                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    /// <summary>The first of <paramref name="names"/> present on PATH, or null.</summary>
    public static string? WhichAny(params string[] names) => names.Select(Which).FirstOrDefault(path => path is not null);
}
