using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Models.Enums;
using OrderProcessing.Shared.Simulation;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public sealed class ProductRepository(JsonFileStore store)
{
    public async Task<Product?> GetByIdAsync(int id, CancellationToken ct)
    {
        return await SimulatedLatency.DatabaseQueryAsync("ProductRepository",
            token => store.GetRecordAsync<Product>(DataFiles.PathFor(DataFiles.Products),
                x => x.Id == id, token), ct);
    }

    public async Task<bool> ReserveStockAsync(int productId, int qty, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(qty);
        bool reserved = false;
        await SimulatedLatency.DatabaseWriteAsync("ProductRepository", async token =>
        {
            reserved = await store.UpdateRecordsAsync<Product>(DataFiles.PathFor(DataFiles.Products), products =>
            {
                Product? product = products.Find(x => x.Id == productId);
                if (product is null || product.StockQuantity < qty)
                    return false;
                product.StockQuantity -= qty;
                return true;
            }, token);
        }, ct);
        return reserved;
    }
}
