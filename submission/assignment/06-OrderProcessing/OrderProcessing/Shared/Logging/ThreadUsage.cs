using System.Collections.Concurrent;

namespace OrderProcessing.Shared.Logging;

public static class ThreadUsage
{
    private static ConcurrentDictionary<int, byte>? _threads;

    public static void Start() => _threads = new();
    public static void Record() => _threads?.TryAdd(Environment.CurrentManagedThreadId, 0);
    public static int Stop() => Interlocked.Exchange(ref _threads, null)?.Count ?? 0;
}
