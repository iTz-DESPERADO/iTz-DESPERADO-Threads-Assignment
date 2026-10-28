using System.Collections.Concurrent;
using System.Diagnostics;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase4;

public sealed class FraudScoring
{
    public static int Score(int orderId)
    {
        long h = orderId;

        for (int i = 0; i < 100_000; i++)
            h = (h * 31 + i) % 1_000_000_007;

        return (int)(h % 100);
    }

    public List<int> ScoreSequential(IEnumerable<Order> orders, CancellationToken ct)
    {
        List<int> flagged = [];

        foreach (Order order in orders)
        {
            ct.ThrowIfCancellationRequested();

            ThreadUsage.Record();

            if (Score(order.Id) >= 90)
                flagged.Add(order.Id);
        }
        return flagged;
    }

    public List<int> ScoreParallel(IEnumerable<Order> orders, CancellationToken ct)
    {
        ConcurrentBag<int> flagged = [];

        Parallel.ForEach(orders, new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = ct
        }, order =>
        {
            ct.ThrowIfCancellationRequested();

            ThreadUsage.Record();

            if (Score(order.Id) >= 90) 
               flagged.Add(order.Id);
        });

        return flagged.Order().ToList();
    }

    public void Run(CancellationToken ct)
    {
        List<Order> orders = Enumerable.Range(1, 10_000).Select(id => new Order { Id = id }).ToList();

        ThreadUsage.Start();

        Stopwatch timer = Stopwatch.StartNew();

        List<int> sequential;

        int sequentialThreads;

        try
        {
             sequential = ScoreSequential(orders, ct);
        }
        finally 
        { 
            sequentialThreads = ThreadUsage.Stop();
        }

        double sequentialSeconds = timer.Elapsed.TotalSeconds;

        int sequentialPool = ThreadPool.ThreadCount;

        ThreadUsage.Start();

        timer.Restart();

        List<int> parallel;

        int parallelThreads;

        try 
        { 
            parallel = ScoreParallel(orders, ct);
        }
        finally 
        { 
            parallelThreads = ThreadUsage.Stop();
        }

        double parallelSeconds = timer.Elapsed.TotalSeconds;

        if (sequential.Count != 1005 || !sequential.SequenceEqual(parallel)
            || !parallel.Take(5).SequenceEqual(new[] { 4, 8, 12, 29, 33 }))
            throw new InvalidOperationException("Fraud results do not match the expected 1005 IDs.");

            
        Log.Info($"Fraud: {parallel.Count} flagged; first five = {string.Join(", ", parallel.Take(5))}");
        Log.Info($"Fraud sequential: {sequentialSeconds:F3} s; distinct threads = {sequentialThreads}; ThreadPool.ThreadCount = {sequentialPool}");
        Log.Info($"Fraud parallel: {parallelSeconds:F3} s; distinct threads = {parallelThreads}; ThreadPool.ThreadCount = {ThreadPool.ThreadCount}");
        Log.Info($"Fraud speed-up: {sequentialSeconds / parallelSeconds:F2}x; Environment.ProcessorCount = {Environment.ProcessorCount}");
    }
}
