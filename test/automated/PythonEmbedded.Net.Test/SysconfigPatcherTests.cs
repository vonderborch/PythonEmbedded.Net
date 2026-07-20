namespace PythonEmbedded.Net.Test;

[TestFixture]
public class SysconfigPatcherTests
{
    // A trimmed but representative excerpt of a real python-build-standalone
    // _sysconfigdata_*.py: double-quoted string values, a single-path value, and a
    // space-separated multi-path value (DESTDIRS), all prefixed with the build machine's
    // baked-in "/install" root.
    private const string SampleContent = """
        # system configuration generated and used by the sysconfig module
        build_time_vars = {
            "BINDIR": "/install/bin",
            "CC": "clang",
            "DESTDIRS": "/install /install/lib /install/lib/python3.13",
            "VERSION": "3.13",
        }
        """;

    [Test]
    public void Patch_Rewrites_Baked_In_Install_Prefix()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("SysconfigPatcher is POSIX-only.");
        }

        using TempRoot root = new();
        string installDir = Path.Combine(root.Path, "installs", "cpython-3.13.14-astral");
        string libDir = Path.Combine(installDir, "python", "lib", "python3.13");
        Directory.CreateDirectory(libDir);
        string sysconfigFile = Path.Combine(libDir, "_sysconfigdata__darwin_darwin.py");
        File.WriteAllText(sysconfigFile, SampleContent);

        Internals.SysconfigPatcher.Patch(installDir);

        string patched = File.ReadAllText(sysconfigFile);
        Assert.That(patched, Does.Contain($"\"BINDIR\": \"{installDir}/bin\""));
        Assert.That(patched, Does.Contain($"\"DESTDIRS\": \"{installDir} {installDir}/lib {installDir}/lib/python3.13\""));
        Assert.That(patched, Does.Contain("\"CC\": \"clang\""), "Only /install-prefixed values should be touched.");
        Assert.That(patched, Does.Not.Contain("/install/"));
    }

    [Test]
    public void Patch_Is_NoOp_When_No_Sysconfigdata_File_Exists()
    {
        using TempRoot root = new();
        string installDir = Path.Combine(root.Path, "installs", "cpython-3.13.14-astral");
        Directory.CreateDirectory(installDir);

        Assert.DoesNotThrow(() => Internals.SysconfigPatcher.Patch(installDir));
    }
}
