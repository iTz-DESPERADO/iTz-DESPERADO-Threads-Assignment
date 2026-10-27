using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Models.Enums;
using OrderProcessing.Shared.Simulation;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public sealed class SyncProductRepository(JsonFileStore store)
{
    public Product? GetById(int id, CancellationToken ct)
    {
        return SimulatedLatency.DatabaseQuery("SyncProductRepository",
            () => store.GetRecord<Product>(DataFiles.PathFor(DataFiles.Products),
                x => x.Id == id), ct);
    }
}
