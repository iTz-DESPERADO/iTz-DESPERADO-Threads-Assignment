using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Models.Enums;
using OrderProcessing.Shared.Simulation;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public sealed class OrderRepository(JsonFileStore store)
{
    public async Task SaveAsync(Order order, CancellationToken ct)
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
