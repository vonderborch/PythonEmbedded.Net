using PythonEmbedded.Net.Internals;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Test;

[TestFixture]
public class DiagnosticsTests
{
    [Test]
    public async Task Healthy_Installation_Reports_No_Errors()
    {
        using TempRoot root = new();
        PythonHost host = new(root.Options(new FakeSource("3.13.5")));
        PythonInstallation install = await host.GetInstallationAsync("3.13", CancellationToken.None);

        DiagnosticsResult result = await install.DiagnoseAsync();

        Assert.That(result.Findings.Where(f => f.Severity == DiagnosticSeverity.Error), Is.Empty);
    }

    [Test]
    public async Task Missing_Executable_Is_Reported_As_An_Error()
    {
        using TempRoot root = new();
        PythonHost host = new(root.Options(new FakeSource("3.13.5")));
        PythonInstallation install = await host.GetInstallationAsync("3.13", CancellationToken.None);

        File.Delete(install.PythonExecutable);

        DiagnosticsResult result = await install.DiagnoseAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsHealthy, Is.False);
            Assert.That(result.Findings, Has.Some.Matches<DiagnosticFinding>(f => f.Code == "executable-missing"));
        });
    }

    [Test]
    public async Task Corrupt_Marker_Is_Reported_As_An_Error()
    {
        using TempRoot root = new();
        PythonHost host = new(root.Options(new FakeSource("3.13.5")));
        PythonInstallation install = await host.GetInstallationAsync("3.13", CancellationToken.None);

        await File.WriteAllTextAsync(Path.Combine(install.Directory, "install.json"), "{not json");

        DiagnosticsResult result = await install.DiagnoseAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsHealthy, Is.False);
            Assert.That(result.Findings, Has.Some.Matches<DiagnosticFinding>(f => f.Code == "marker-invalid"));
        });
    }

    [Test]
    public async Task Stale_Lock_File_Is_Reported_As_A_Warning()
    {
        using TempRoot root = new();
        PythonHost host = new(root.Options(new FakeSource("3.13.5")));
        PythonInstallation install = await host.GetInstallationAsync("3.13", CancellationToken.None);

        string staleLock = Path.Combine(host.LocksDirectory, "stale.lock");
        await File.WriteAllTextAsync(staleLock, string.Empty);
        File.SetLastWriteTimeUtc(staleLock, DateTime.UtcNow.AddDays(-2));

        DiagnosticsResult result = await install.DiagnoseAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsHealthy, Is.True, "a stale lock is a warning, not an error");
            Assert.That(result.Findings, Has.Some.Matches<DiagnosticFinding>(
                f => f.Code == "stale-lock" && f.Severity == DiagnosticSeverity.Warning));
        });
    }

    [Test]
    public async Task Healthy_Environment_Reports_No_Errors()
    {
        using TempRoot root = new();
        PythonOptions options = root.Options(new FakeSource("3.13.5"));
        options.Installer = new FakeInstaller();
        PythonHost host = new(options);
        PythonVirtualEnvironment env = await host.GetEnvironmentAsync("3.13", "default", CancellationToken.None);

        DiagnosticsResult result = await env.DiagnoseAsync();

        Assert.That(result.Findings.Where(f => f.Severity == DiagnosticSeverity.Error), Is.Empty);
    }

    [Test]
    public async Task Environment_Missing_Executable_Is_Reported_As_An_Error()
    {
        using TempRoot root = new();
        PythonOptions options = root.Options(new FakeSource("3.13.5"));
        options.Installer = new FakeInstaller();
        PythonHost host = new(options);
        PythonVirtualEnvironment env = await host.GetEnvironmentAsync("3.13", "default", CancellationToken.None);

        File.Delete(env.PythonExecutable);

        DiagnosticsResult result = await env.DiagnoseAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsHealthy, Is.False);
            Assert.That(result.Findings, Has.Some.Matches<DiagnosticFinding>(f => f.Code == "executable-missing"));
        });
    }
}
