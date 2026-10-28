using System.Diagnostics;
using OrderProcessing.Phase1;
using OrderProcessing.Phase3;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Models.Enums;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase4;

public sealed class Phase4Runner(JsonFileStore store)
{
    public async Task RunAsync(CancellationToken ct)
    {
        bool initialized = false;
        try
        {
            await new DataSeeder(store).ResetAsync(ct);

            initialized = true;

            List<Order> input = await new IncomingOrderRepository(store).GetAllAsync(ct);

            await MeasureAsync("Live checkout", token => RunCheckoutAsync(input, token), ct);
            
            ct.ThrowIfCancellationRequested();

            CustomerDashboardService dashboards = new(store, [99]);

            foreach (int id in new[] { 3, 99 })
            {
                await MeasureAsync($"Dashboard #{id}", async token =>
                {
                    try
                    {
                        CustomerDashboard dashboard = await dashboards.LoadAsync(id, token);

                        Log.Info($"{dashboard.Customer.Name}: orders = {dashboard.Orders.Count}, payments = {dashboard.Payments.Count}, emails = {dashboard.Emails.Count}");

                        foreach (Order order in dashboard.Orders.OrderBy(x => x.Id)) 
                            Log.Info($"Dashboard order #{order.Id}: {order.Status}");

                        foreach (PaymentTransaction payment in dashboard.Payments.OrderBy(x => x.OrderId)) 
                            Log.Info($"Dashboard payment #{payment.OrderId}: {payment.Status}");

                        foreach (EmailMessage email in dashboard.Emails) 
                            Log.Info($"Dashboard email #{email.OrderId}");
                    }
                    catch (AggregateException ex)
                    {
                        foreach (Exception error in ex.Flatten().InnerExceptions) 
                            Log.Error($"Dashboard #{id}: {error.Message}");
                    }
                }, ct);
            }

            ShippingQuoteService shipping = new([new FastShip(store, []), new CairoExpress(store, []), new NileCourier(store, [])]);

            foreach (Order order in input.Take(5))
            {
                await MeasureAsync($"Shipping #{order.Id}", async token =>
                {
                    ShippingQuote quote = await shipping.GetFastestQuoteAsync(order, token);

                    Log.Info($"Order #{order.Id}: {quote.Carrier}, price = {quote.Price:F2}, time = {quote.ElapsedMs} ms; cancelled = {string.Join(", ", shipping.CancelledCarriers)}");
                }, ct);
            }
            await MeasureAsync("Shipping all-failed demo", async token =>
            {
                ShippingQuoteService failing = new([new FastShip(store, [6]), new CairoExpress(store, [6]), new NileCourier(store, [6])]);

                try 
                { 
                    await failing.GetFastestQuoteAsync(input.Single(x => x.Id == 6), token);
                }
                catch (ShippingProviderUnavailableException ex) 
                { 
                    Log.Error($"Order #6: {ex.Message}");
                }

            }, ct);

            new FraudScoring().Run(ct);

            await MeasureAsync("Email re-send", token => new EmailResend(new EmailService(store,
                new Dictionary<int, int>(), disableFailures: true)).RunAsync(token), ct);

        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            using CancellationTokenSource cleanup = new();

            List<Order> saved = initialized
                ? await store.GetAllRecordsAsync<Order>(DataFiles.PathFor(DataFiles.Orders), cleanup.Token) : [];

            Log.Info($"Run cancelled — {saved.Count(x => x.Status != OrderStatus.Pending)} orders finished, " +
                $"{saved.Count(x => x.Status == OrderStatus.Pending && x.FailureReason == "Cancelled")} cancelled.");
        }
    }

    private async Task RunCheckoutAsync(List<Order> input, CancellationToken ct)
    {
        Phase3Runner checkout = new(store);

        List<Task<(Order Order, double Seconds)>> tasks = [];

        foreach (Order order in input)
        {
            if (ct.IsCancellationRequested) 
               break;

            tasks.Add(ProcessAsync(checkout.CreateProcessor(), order, ct));
        }
        int count = 0;

        await foreach (Task<(Order Order, double Seconds)> completed in Task.WhenEach(tasks))
        {
            var result = await completed;

            Order order = result.Order;

            string reason = order.FailureReason is null ? "" : $" ({order.FailureReason})";

            Log.Info($"[{++count,2}/{input.Count}] Order #{order.Id} -> {order.Status}{reason} after {result.Seconds:F1} s");
        }

        List<Order> saved = await new OrderRepository(store).GetAllAsync(ct);

        foreach (var group in saved.GroupBy(x => x.Status).OrderBy(x => x.Key))
            Log.Info($"Live checkout {group.Key}: {group.Count()}");
    }

    private async Task<(Order Order, double Seconds)> ProcessAsync(OrderProcessor processor, Order order, CancellationToken ct)
    {
        Stopwatch timer = Stopwatch.StartNew();

        try 
        { 
            await processor.ProcessOrderAsync(order, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            using CancellationTokenSource cleanup = new();

            order.Status = OrderStatus.Pending;

            order.FailureReason = "Cancelled";

            await new OrderRepository(store).SaveAsync(order, cleanup.Token);
        }
        return (order, timer.Elapsed.TotalSeconds);
    }

    private static async Task MeasureAsync(string name, Func<CancellationToken, Task> operation, CancellationToken ct)
    {
        ThreadUsage.Start();

        Stopwatch timer = Stopwatch.StartNew();

        try 
        { 
            await operation(ct);
        }
        finally
        {
            int threads = ThreadUsage.Stop();
            
            Log.Info($"{name}: {timer.Elapsed.TotalSeconds:F3} s; distinct logged threads = {threads}; ThreadPool.ThreadCount = {ThreadPool.ThreadCount}");
        }
    }
}
