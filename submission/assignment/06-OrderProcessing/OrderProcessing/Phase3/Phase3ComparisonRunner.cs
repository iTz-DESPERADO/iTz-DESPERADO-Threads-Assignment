using System.Diagnostics;
using OrderProcessing.Phase1;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Models.Enums;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public sealed class Phase3ComparisonRunner(JsonFileStore store)
{
    public async Task RunAsync(CancellationToken ct)
    {
        try
        {
            await new DataSeeder(store).ResetAsync(ct);

            List<Order> batch = (await new IncomingOrderRepository(store).GetAllAsync(ct)).Take(10).ToList();

            List<Thread> threads = [];

            ThreadUsage.Start();

            int poolBefore = ThreadPool.ThreadCount;

            Stopwatch timer = Stopwatch.StartNew();

            foreach (Order order in batch)
            {
                if (ct.IsCancellationRequested)
                    break;

                SyncOrderProcessor processor = new(new(new(store)), new(store), new(store, new(store), new(store)),
                    new(store), new(store), new(store));

                Thread thread = new(() => processor.ProcessOrder(order, ct));

                threads.Add(thread);

                thread.Start();
            }

            foreach (Thread thread in threads) 
                thread.Join();

            timer.Stop();

            int observed = ThreadUsage.Stop();

            Log.Info($"Sync comparison: {timer.Elapsed.TotalSeconds:F3} seconds; dedicated threads = {threads.Count}; " +
                $"observed workflow threads = {observed}; ThreadPool.ThreadCount = {poolBefore} -> {ThreadPool.ThreadCount}");

            ct.ThrowIfCancellationRequested();

            await PrintCountsAsync("Sync", ct);

            await new DataSeeder(store).ResetAsync(ct);

            batch = (await new IncomingOrderRepository(store).GetAllAsync(ct)).Take(10).ToList();

            Phase3Runner runner = new(store, disableFailures: true);

            List<Task<bool>> tasks = [];

            ThreadUsage.Start();

            poolBefore = ThreadPool.ThreadCount;

            timer.Restart();

            foreach (Order order in batch)
            {
                if (ct.IsCancellationRequested) 
                    break;
                tasks.Add(runner.CreateProcessor().ProcessOrderAsync(order, ct));
            }

            foreach (Task<bool> task in tasks)
            {
                try 
                { 
                    await task; 
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            }

            timer.Stop();

            observed = ThreadUsage.Stop();

            Log.Info($"Async comparison: {timer.Elapsed.TotalSeconds:F3} seconds; dedicated threads = 0; " +
                $"observed workflow threads = {observed}; ThreadPool.ThreadCount = {poolBefore} -> {ThreadPool.ThreadCount}");

            ct.ThrowIfCancellationRequested();

            await PrintCountsAsync("Async", ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            using CancellationTokenSource cleanup = new();

            List<Order> saved = await store.GetAllRecordsAsync<Order>(DataFiles.PathFor(DataFiles.Orders), cleanup.Token);

            Log.Info($"Run cancelled — {saved.Count(x => x.Status != OrderStatus.Pending)} orders finished, " +
                $"{saved.Count(x => x.FailureReason == "Cancelled")} cancelled.");
        }
        finally 
        { 
            ThreadUsage.Stop(); 
        }
    }

    private async Task PrintCountsAsync(string version, CancellationToken ct)
    {
        List<Order> saved = await new OrderRepository(store).GetAllAsync(ct);

        Phase3Runner.PrintTable(saved);

        int completed = saved.Count(x => x.Status == OrderStatus.Completed);

        int rejected = saved.Count(x => x.Status == OrderStatus.Rejected);

        Log.Info($"{version} comparison: {completed} Completed, {rejected} Rejected");

        if (completed != 8 || rejected != 2 || saved.Count != 10)
            throw new InvalidOperationException("Comparison must produce 8 Completed and 2 Rejected.");
    }
}
