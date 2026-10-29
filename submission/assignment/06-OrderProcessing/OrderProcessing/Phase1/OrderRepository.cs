using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Models.Enums;
using OrderProcessing.Shared.Simulation;
using OrderProcessing.Shared.Storage;
using OrderProcessing.Shared.Exceptions;

namespace OrderProcessing.Phase1;

public sealed class OrderRepository(JsonFileStore store, IEnumerable<int>? failSaveForOrderIds = null,
    bool disableFailures = false)
{
    private readonly HashSet<int> _failSaveForOrderIds = disableFailures ? [] : new(failSaveForOrderIds ?? []);

    public async Task SaveAsync(Order order, CancellationToken ct = default)
    {
        await SimulatedLatency.DatabaseWriteAsync("OrderRepository", async token =>
        {
            await store.UpdateRecordsAsync<Order>(DataFiles.PathFor(DataFiles.Orders), orders =>
            {
                int index = orders.FindIndex(x => x.Id == order.Id);

                if (index < 0)
                    orders.Add(order);
                else
                    orders[index] = order;

                return true;
                
            }, token);
        }, ct);

        if (order.Status == OrderStatus.Completed && _failSaveForOrderIds.Contains(order.Id))
            throw new DatabaseWriteException("Connection lost during commit");
    }

    public async Task<List<Order>> GetAllAsync(CancellationToken ct)
    {
        return await SimulatedLatency.DatabaseQueryAsync("OrderRepository",
            token => store.GetAllRecordsAsync<Order>(DataFiles.PathFor(DataFiles.Orders), token), ct);
    }

    public async Task<List<Order>> GetByStatusAsync(OrderStatus status, CancellationToken ct)
    {
        List<Order> orders = await GetAllAsync(ct);

        return orders.Where(x => x.Status == status).ToList();
    }
}
