using PythonEmbedded.Net.Internals;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Test;

/// <summary>A synchronous <see cref="IProgress{T}"/> that records reports in order (no thread-pool posting).</summary>
public sealed class RecordingProgress<T> : IProgress<T>
{
    public List<T> Reports { get; } = [];

    public void Report(T value) => Reports.Add(value);
}

[TestFixture]
public class InstallProgressTests
{
    [Test]
    public async Task Install_Reports_Phases_In_Order()
    {
        using TempRoot root = new();
        FakeSource source = new("3.13.5");
        PythonHost host = new(root.Options(source));
        RecordingProgress<InstallProgress> progress = new();

        await host.GetInstallationAsync("3.13", CancellationToken.None, progress);

        List<InstallPhase> phases = progress.Reports.Select(p => p.Phase).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(phases, Does.Contain(InstallPhase.WaitingForLock));
            Assert.That(phases, Does.Contain(InstallPhase.ResolvingMetadata));
            Assert.That(phases, Does.Contain(InstallPhase.Extracting));
            Assert.That(phases, Does.Contain(InstallPhase.Committing));
            Assert.That(phases, Does.Contain(InstallPhase.Patching));
            Assert.That(phases.IndexOf(InstallPhase.WaitingForLock), Is.LessThan(phases.IndexOf(InstallPhase.ResolvingMetadata)));
            Assert.That(phases.IndexOf(InstallPhase.Committing), Is.LessThan(phases.IndexOf(InstallPhase.Patching)));
        });
    }

    [Test]
    public async Task Reused_Installation_Reports_No_Phases()
    {
        using TempRoot root = new();
        FakeSource source = new("3.13.5");
        PythonHost host = new(root.Options(source));
        await host.GetInstallationAsync("3.13", CancellationToken.None);

        RecordingProgress<InstallProgress> progress = new();
        await host.GetInstallationAsync("3.13", CancellationToken.None, progress);

        Assert.That(progress.Reports, Is.Empty);
    }
}
