using System.Diagnostics;
using OrderProcessing.Phase3;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase4;

public sealed class EmailResend(IEmailService emails)
{
    public async Task RunAsync(CancellationToken ct)
    {
        List<Order> testOrders = Enumerable.Range(1001, 30).Select(id => new Order { Id = id }).ToList();

        Customer recipient = new() { Name = "Test Customer", Email = "test.customer@example.com" };

        int active = 0;

        int peak = 0;

        Stopwatch timer = Stopwatch.StartNew();

        await Parallel.ForEachAsync(testOrders, new ParallelOptions
        {
            MaxDegreeOfParallelism = 4,
            CancellationToken = ct
        }, async (order, token) =>
        {
            int running = Interlocked.Increment(ref active);

            int previous;

            do
            {
                previous = Volatile.Read(ref peak);

                if (running <= previous) 
                break;

            } while (Interlocked.CompareExchange(ref peak, running, previous) != previous);

            try 
            { 
                await emails.SendConfirmationAsync(order, recipient, token);
            }
            finally 
            { 
                Interlocked.Decrement(ref active);
            }
            
        });

        Log.Info($"Email re-send: 30 sent; peak concurrent sends = {peak}; time = {timer.Elapsed.TotalSeconds:F3} s");

        if (peak != 4) 
           throw new InvalidOperationException("Expected exactly four concurrent sends.");

    }
}
