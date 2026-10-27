using System.Diagnostics;
using OrderProcessing.Shared.Cancellation;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Models.Enums;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public sealed class Phase1Runner(JsonFileStore store)
{
    public async Task RunAsync(CancellationToken ct)
    {
        DataSeeder seeder = new(store);

        IncomingOrderRepository incoming = new(store);

        OrderRepository orders = new(store);

        SyncOrderValidator syncValidator = new(new(store), new(store), new(store));

        OrderValidator validator = new(new(store), new(store), new(store));

        Order? current = null;

        int finished = 0;

        try
        {
            await seeder.ResetAsync(ct);

            List<Order> batch = await incoming.GetAllAsync(ct);

            Log.Info("Starting synchronous validation (results are not saved).");

            Stopwatch timer = Stopwatch.StartNew();

            foreach (Order order in batch)
            {
                ct.ThrowIfCancellationRequested();

                current = order;

                syncValidator.ValidateOrder(order, ct);

                Log.Info($"Sync order #{order.Id}: {order.Status} {order.FailureReason}");

                finished++;

                current = null;
            }

            timer.Stop();

            TimeSpan syncTime = timer.Elapsed;

            await seeder.ResetAsync(ct);

            finished = 0;

            batch = await incoming.GetAllAsync(ct);

            Log.Info("Starting sequential asynchronous validation and saving.");

            timer.Restart();

            TimeSpan validationTime = TimeSpan.Zero;

            foreach (Order order in batch)
            {
                ct.ThrowIfCancellationRequested();

                current = order;

                Stopwatch validationTimer = Stopwatch.StartNew();

                await validator.ValidateOrderAsync(order, ct);

                validationTime += validationTimer.Elapsed;

                await orders.SaveAsync(order, ct);

                Log.Info($"Async order #{order.Id}: {order.Status} {order.FailureReason}");

                finished++;

                current = null;
            }

            timer.Stop();

            List<Order> validated = await orders.GetByStatusAsync(OrderStatus.Validated, ct);

            List<Order> rejected = await orders.GetByStatusAsync(OrderStatus.Rejected, ct);

            var totalCount = validated.Count + rejected.Count;

            Console.WriteLine("===== Validation Summary =====");
            Console.WriteLine($"Total Orders: {totalCount}");
            Console.WriteLine($"Validated:    {validated.Count}");
            Console.WriteLine($"Rejected:     {rejected.Count}");
            foreach (Order order in rejected.OrderBy(x => x.Id))
                Console.WriteLine($"  - Order #{order.Id}: {order.FailureReason}");
            Console.WriteLine($"Sync Time:  {syncTime.TotalSeconds:F1} seconds");
            Console.WriteLine($"Async Time: {timer.Elapsed.TotalSeconds:F1} seconds");
            Console.WriteLine($"Async validation only: {validationTime.TotalSeconds:F1} seconds");
            Console.WriteLine($"orders.json now contains {totalCount} records.");

        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            int cancelled = 0;

            if (current is not null)
            {
                using CancellationTokenSource cleanup = new();

                await new OrderCancellationCleanup(store).CancelOrderAsync(current, [], null, cleanup.Token);

                cancelled = 1;
            }
            
            Log.Info($"Run cancelled — {finished} orders finished, {cancelled} cancelled.");
        }
    }
}
