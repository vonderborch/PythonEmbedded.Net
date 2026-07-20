namespace PythonEmbedded.Net.Test;

[TestFixture]
public class DiskLockTests
{
    [Test]
    public async Task Second_Acquire_Times_Out_While_Held()
    {
        using TempRoot root = new();
        string lockPath = Path.Combine(root.Path, "locks", "test.lock");

        using DiskLock held = await DiskLock.AcquireAsync(lockPath, TimeSpan.FromSeconds(5), CancellationToken.None);

        PythonException ex = Assert.ThrowsAsync<PythonException>(
            () => DiskLock.AcquireAsync(lockPath, TimeSpan.FromMilliseconds(300), CancellationToken.None))!;
        Assert.That(ex.Kind, Is.EqualTo(PythonErrorKind.Locked));
    }

    [Test]
    public async Task Release_Allows_Reacquire()
    {
        using TempRoot root = new();
        string lockPath = Path.Combine(root.Path, "locks", "test.lock");

        DiskLock first = await DiskLock.AcquireAsync(lockPath, TimeSpan.FromSeconds(5), CancellationToken.None);
        first.Dispose();

        using DiskLock second = await DiskLock.AcquireAsync(lockPath, TimeSpan.FromMilliseconds(500), CancellationToken.None);
        Assert.That(second, Is.Not.Null);
    }

    [Test]
    public async Task Waiter_Proceeds_When_Holder_Releases()
    {
        using TempRoot root = new();
        string lockPath = Path.Combine(root.Path, "locks", "test.lock");

        DiskLock held = await DiskLock.AcquireAsync(lockPath, TimeSpan.FromSeconds(5), CancellationToken.None);
        Task<DiskLock> waiter = DiskLock.AcquireAsync(lockPath, TimeSpan.FromSeconds(10), CancellationToken.None);

        await Task.Delay(250);
        Assert.That(waiter.IsCompleted, Is.False, "waiter must block while the lock is held");

        held.Dispose();
        using DiskLock acquired = await waiter.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(acquired, Is.Not.Null);
    }
}
