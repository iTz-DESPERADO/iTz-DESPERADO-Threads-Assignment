using OrderProcessing.Phase1;
using OrderProcessing.Phase2;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Models.Enums;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public sealed class Phase3Runner(JsonFileStore store, bool disableFailures = false)
{
    public OrderProcessor CreateProcessor()
    {
        return new OrderProcessor(new CustomerService(new CustomerRepository(store)), new InventoryService(store),
            new PricingService(store, new(store), new(store), []),
            new PaymentGateway(store, [5], [8], disableFailures),
            new OrderRepository(store, [13], disableFailures),
            new EmailRetryService(new EmailService(store, new Dictionary<int, int>
                { [3] = 2, [17] = int.MaxValue }, disableFailures), store));
    }

    public async Task RunAsync(CancellationToken ct)
    {
        bool initialized = false;

        try
        {
            await new DataSeeder(store).ResetAsync(ct);

            initialized = true;

            List<Order> input = await new IncomingOrderRepository(store).GetAllAsync(ct);

            List<Task<bool>> tasks = [];

            foreach (Order order in input)
            {
                if (ct.IsCancellationRequested)
                    break;

                tasks.Add(CreateProcessor().ProcessOrderAsync(order, ct));
            }

            foreach (Task<bool> task in tasks)
            {
                try 
                { 
                    await task;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            }

            ct.ThrowIfCancellationRequested();

            List<Order> saved = await new OrderRepository(store).GetAllAsync(ct);

            PrintTable(saved);

        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            using CancellationTokenSource cleanup = new();

            List<Order> saved = initialized
                ? await store.GetAllRecordsAsync<Order>(DataFiles.PathFor(DataFiles.Orders), cleanup.Token)
                : [];

            PrintTable(saved);

            Log.Info($"Run cancelled — {saved.Count(x => x.Status != OrderStatus.Pending)} orders finished, " +
                $"{saved.Count(x => x.Status == OrderStatus.Pending && x.FailureReason == "Cancelled")} cancelled.");
        }
    }

    internal static void PrintTable(List<Order> orders)
    {
        Console.WriteLine("Order | Status        | Total    | Reason");

        foreach (Order order in orders.OrderBy(x => x.Id))
            Console.WriteLine($"{order.Id,5} | {order.Status,-13} | {order.TotalAmount,8:F2} | {order.FailureReason}");

        foreach (var group in orders.GroupBy(x => x.Status).OrderBy(x => x.Key))
            Console.WriteLine($"{group.Key}: {group.Count()}");
    }
}
