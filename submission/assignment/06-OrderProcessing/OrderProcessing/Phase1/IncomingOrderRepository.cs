using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Models.Enums;
using OrderProcessing.Shared.Simulation;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public sealed class IncomingOrderRepository(JsonFileStore store)
{
    public async Task<List<Order>> GetAllAsync(CancellationToken ct)
    {
        return await SimulatedLatency.DatabaseQueryAsync("IncomingOrderRepository",
            token => store.GetAllRecordsAsync<Order>(DataFiles.PathFor(DataFiles.IncomingOrders), token), ct);
    }
}
