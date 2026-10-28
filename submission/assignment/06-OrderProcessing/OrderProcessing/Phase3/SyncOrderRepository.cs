using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Simulation;
using OrderProcessing.Shared.Storage;
namespace OrderProcessing.Phase3;

public sealed class SyncOrderRepository(JsonFileStore store)
{
    public void Save(Order order, CancellationToken ct)
    {
        SimulatedLatency.DatabaseWrite("SyncOrderRepository", () =>
        {
            store.UpdateRecords<Order>(DataFiles.PathFor(DataFiles.Orders), orders =>
            {
                int index = orders.FindIndex(x => x.Id == order.Id);

                if (index < 0) 
                    orders.Add(order);

                else 
                    orders[index] = order;

                return true;
            });
        }, ct);
    }
}
