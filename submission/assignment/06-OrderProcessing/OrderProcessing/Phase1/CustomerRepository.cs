using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Models.Enums;
using OrderProcessing.Shared.Simulation;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public sealed class CustomerRepository(JsonFileStore store)
{
    public async Task<Customer?> GetByIdAsync(int id, CancellationToken ct)
    {
        return await SimulatedLatency.DatabaseQueryAsync("CustomerRepository",
            token => store.GetRecordAsync<Customer>(DataFiles.PathFor(DataFiles.Customers),
                x => x.Id == id, token), ct);
    }
}
