using System.Diagnostics;
using System.Text.Json;
using OrderProcessing.Phase1;
using OrderProcessing.Shared.Cancellation;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Models.Enums;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public sealed class Phase2Runner(JsonFileStore store)
{
    public async Task RunAsync(CancellationToken ct)
    {
        DataSeeder seeder = new(store);

        ProductRepository products = new(store);

        OrderRepository orders = new(store);

        IncomingOrderRepository incoming = new(store);

        OrderValidator validator = new(new(store), products, new(store));

        OrderPricer pricer = new(new PricingService(store, new(store), new(store), [7]));

        OrderCancellationCleanup cancellationCleanup = new(store);

        try
        {
            await seeder.ResetAsync(ct);

            await new RaceConditionDemo(store, products).RunAsync(ct);

            await seeder.ResetAsync(ct);

            List<Order> batch = await incoming.GetAllAsync(ct);

            Order source = batch.Single(x => x.Id == 1);

            Order comparison = new()
            {
                Id = source.Id,
                CustomerId = source.CustomerId,
                CouponCode = source.CouponCode,
                Items = source.Items.Select(x => new OrderItem
                    { ProductId = x.ProductId, Quantity = x.Quantity }).ToList()
            };

            Stopwatch timer = Stopwatch.StartNew();

            await pricer.PriceOrderSequentialAsync(comparison, ct);

            Log.Info($"Sequential pricing #1: {timer.Elapsed.TotalSeconds:F3} seconds; total = {comparison.TotalAmount:F2}");

            timer.Restart();

            await pricer.PriceOrderAsync(comparison, ct);

            Log.Info($"Concurrent pricing #1: {timer.Elapsed.TotalSeconds:F3} seconds; total = {comparison.TotalAmount:F2}");

            timer.Restart();

            List<Task> validations = [];

            foreach (Order order in batch)
            {
                if (ct.IsCancellationRequested)
                    break;
                validations.Add(ValidateAndSaveAsync(order, validator, orders, cancellationCleanup, ct));
            }

            foreach (Task validation in validations)
                await validation;
            ct.ThrowIfCancellationRequested();

            List<Order> validated = batch.Where(x => x.Status == OrderStatus.Validated).ToList();

            List<Task<bool>> tasks = [];

            List<OrderProcessorV2> processors = [];

            foreach (Order order in validated)
            {
                if (ct.IsCancellationRequested)
                    break;

                OrderProcessorV2 processor = new(products, orders, pricer, new InvoiceGenerator());

                processors.Add(processor);

                tasks.Add(processor.ProcessValidatedOrderAsync(order, ct));

                if (tasks.Count == 1)
                    Log.Info($"task[0] immediately: {tasks[0].Status}");
            }

            try
            {
                await Task.Delay(1000, ct);

                for (int i = 0; i < tasks.Count; i++)
                    Log.Info($"Order #{validated[i].Id} task while running: {tasks[i].Status}");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                
            }

            int index = 0;

            foreach (Task<bool> task in tasks)
            {
                Order order = validated[index];

                OrderProcessorV2 processor = processors[index];
                try
                {
                    await task;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    using CancellationTokenSource cleanup = new();

                    await cancellationCleanup.CancelOrderAsync(order, processor.ReservedItems, null, cleanup.Token);
                }
                catch (Exception ex)
                {
                    Log.Error($"Order #{order.Id} task: {task.Status}; {ex.Message}");

                    using CancellationTokenSource cleanup = new();

                    await processor.ReleaseReservedStockAsync(cleanup.Token);

                    order.Status = OrderStatus.Failed;

                    order.FailureReason = ex.Message;

                    await orders.SaveAsync(order, cleanup.Token);
                }

                index++;
            }
            for (int i = 0; i < tasks.Count; i++)
                Log.Info($"Order #{validated[i].Id} final task status: {tasks[i].Status}");

            timer.Stop();

            ct.ThrowIfCancellationRequested();

            Log.Info($"Phase 2 validation + processing time: {timer.Elapsed.TotalSeconds:F3} seconds");

            List<Order> saved = await orders.GetAllAsync(ct);

            Log.Info($"orders.json contains {saved.Count} records (expected 20).");

            if (saved.Count != 20 || saved.Select(x => x.Id).Distinct().Count() != 20)
                throw new InvalidOperationException("Expected 20 unique saved orders.");

            Task<Report> reportTask = new ReportGenerator(orders, products).GenerateAsync(ct);

            Console.WriteLine("Generating report...");
            
            Report report = await reportTask;

            Console.WriteLine(JsonSerializer.Serialize(report, JsonSettings.Options));

            BlockingDemo.Run(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            using CancellationTokenSource cleanup = new();

            List<Order> saved = await store.GetAllRecordsAsync<Order>(DataFiles.PathFor(DataFiles.Orders), cleanup.Token);

            int cancelled = saved.Count(x => x.Status == OrderStatus.Pending && x.FailureReason == "Cancelled");

            int finished = saved.Count(x => x.Status != OrderStatus.Pending);
            
            Log.Info($"Run cancelled — {finished} orders finished, {cancelled} cancelled.");
        }
    }

    private static async Task ValidateAndSaveAsync(Order order, OrderValidator validator,
        OrderRepository orders, OrderCancellationCleanup cancellationCleanup, CancellationToken ct)
    {
        try
        {
            await validator.ValidateOrderAsync(order, ct);
            await orders.SaveAsync(order, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            using CancellationTokenSource cleanup = new();
            await cancellationCleanup.CancelOrderAsync(order, [], null, cleanup.Token);
        }
    }
}
