using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Models.Enums;
using OrderProcessing.Shared.Simulation;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public sealed class SyncCustomerRepository(JsonFileStore store)
{
    public Customer? GetById(int id, CancellationToken ct)
    {
        return SimulatedLatency.DatabaseQuery("SyncCustomerRepository",
            () => store.GetRecord<Customer>(DataFiles.PathFor(DataFiles.Customers),
                x => x.Id == id), ct);
    }
}
