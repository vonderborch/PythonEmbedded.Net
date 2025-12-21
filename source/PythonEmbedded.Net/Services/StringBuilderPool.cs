using System.Collections.Concurrent;
using System.Text;

namespace PythonEmbedded.Net.Services;

/// <summary>
/// Provides a pool of StringBuilder instances to reduce allocations.
/// </summary>
internal static class StringBuilderPool
{
    private static readonly ObjectPool<StringBuilder> _pool = new(() => new StringBuilder(), sb => sb.Clear());

    /// <summary>
    /// Gets a StringBuilder from the pool.
    /// </summary>
    public static StringBuilder Get() => _pool.Get();

    /// <summary>
    /// Returns a StringBuilder to the pool.
    /// </summary>
    public static void Return(StringBuilder sb) => _pool.Return(sb);
}

/// <summary>
/// Simple object pool implementation for reusable objects.
/// </summary>
internal class ObjectPool<T> where T : class
{
    private readonly Func<T> _factory;
    private readonly Action<T>? _reset;
    private readonly ConcurrentQueue<T> _items = new();

    public ObjectPool(Func<T> factory, Action<T>? reset = null)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _reset = reset;
    }

    public T Get()
    {
        if (_items.TryDequeue(out var item))
        {
            return item;
        }
        return _factory();
    }

    public void Return(T item)
    {
        if (item == null) return;
        _reset?.Invoke(item);
        _items.Enqueue(item);
    }
}

