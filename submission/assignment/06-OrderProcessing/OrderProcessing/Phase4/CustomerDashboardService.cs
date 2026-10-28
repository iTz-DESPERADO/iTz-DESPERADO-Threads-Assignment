using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Simulation;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase4;

public sealed class CustomerDashboardService(JsonFileStore store, IEnumerable<int> failPaymentsForCustomerIds)
{
    private readonly HashSet<int> _failPayments = new(failPaymentsForCustomerIds);

    public async Task<CustomerDashboard> LoadAsync(int customerId, CancellationToken ct)
    {
        CustomerDashboard dashboard = new();

        Task profile = LoadPartAsync("Dashboard profile", async token =>
            await store.GetRecordAsync<Customer>(DataFiles.PathFor(DataFiles.Customers), x => x.Id == customerId, token)
                ?? throw new CustomerNotFoundException($"Customer #{customerId} not found"),
            value => dashboard.Customer = value, ct);

        Task orders = LoadPartAsync("Dashboard orders", async token =>
            (await store.GetAllRecordsAsync<Order>(DataFiles.PathFor(DataFiles.Orders), token))
                .Where(x => x.CustomerId == customerId).ToList(), value => dashboard.Orders = value, ct);

        Task payments = LoadPartAsync("Dashboard payments", async token =>
        {
            if (_failPayments.Contains(customerId)) 
            throw new TimeoutException("Payments store timeout");

            HashSet<int> ids = await CustomerOrderIdsAsync(customerId, token);

            return (await store.GetAllRecordsAsync<PaymentTransaction>(DataFiles.PathFor(DataFiles.Payments), token))
                .Where(x => ids.Contains(x.OrderId)).ToList();

        }, value => dashboard.Payments = value, ct);


        Task emails = LoadPartAsync("Dashboard emails", async token =>
        {
            HashSet<int> ids = await CustomerOrderIdsAsync(customerId, token);

            return (await store.GetAllRecordsAsync<EmailMessage>(DataFiles.PathFor(DataFiles.Emails), token))
                .Where(x => ids.Contains(x.OrderId)).ToList();

        }, value => dashboard.Emails = value, ct);


        Task all = Task.WhenAll(profile, orders, payments, emails);

        try 
        { 
            await all;
        }
        catch
        {
            ct.ThrowIfCancellationRequested();
            
            throw all.Exception!.Flatten();
        }
        return dashboard;
    }

    private async Task<HashSet<int>> CustomerOrderIdsAsync(int customerId, CancellationToken ct)
    {
        return (await store.GetAllRecordsAsync<Order>(DataFiles.PathFor(DataFiles.IncomingOrders), ct))
            .Where(x => x.CustomerId == customerId).Select(x => x.Id).ToHashSet();
    }

    private static async Task LoadPartAsync<T>(string name, Func<CancellationToken, Task<T>> read,
        Action<T> assign, CancellationToken ct)
    {
        assign(await SimulatedLatency.DatabaseQueryAsync(name, read, ct));
    }
}
