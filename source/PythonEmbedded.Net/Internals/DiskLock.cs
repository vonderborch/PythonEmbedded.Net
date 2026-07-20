namespace PythonEmbedded.Net;

/// <summary>
/// A cross-process exclusive lock backed by a file opened with <see cref="FileShare.None"/>.
/// Dispose to release. The lock file is deleted on close.
/// </summary>
internal sealed class DiskLock : IDisposable
{
    private readonly FileStream _stream;

    private DiskLock(FileStream stream)
    {
        _stream = stream;
    }

    public static async Task<DiskLock> AcquireAsync(string lockFilePath, TimeSpan timeout, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(lockFilePath)!);
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                FileStream stream = new(
                    lockFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None,
                    bufferSize: 1, FileOptions.DeleteOnClose);
                return new DiskLock(stream);
            }
            catch (IOException) when (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(100, ct).ConfigureAwait(false);
            }
            catch (IOException)
            {
                throw new PythonException(
                    PythonErrorKind.Locked,
                    $"Could not acquire lock '{Path.GetFileName(lockFilePath)}' within {timeout.TotalSeconds:0}s. " +
                    "Another process may be installing; retry later or increase PythonOptions.LockTimeout.");
            }
        }
    }

    public void Dispose() => _stream.Dispose();
}
