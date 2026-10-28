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
        return await ReserveStockAsync(productId, qty, () => { }, ct);
    }

    internal async Task<bool> ReserveStockAsync(int productId, int qty, Action onReserved, CancellationToken ct)
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

            if (reserved)
                onReserved();
                
        }, ct);

        return reserved;
    }

    public async Task ReleaseStockAsync(int productId, int qty, CancellationToken ct)
    {
        await ReleaseStockAsync(productId, qty, () => { }, ct);
    }

    internal async Task ReleaseStockAsync(int productId, int qty, Action onReleased, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(qty);

        await SimulatedLatency.DatabaseWriteAsync("ProductRepository", async token =>
        {
            await store.UpdateRecordsAsync<Product>(DataFiles.PathFor(DataFiles.Products), products =>
            {
                Product product = products.Find(x => x.Id == productId)
                    ?? throw new InvalidOperationException($"Product #{productId} not found");

                product.StockQuantity += qty;

                return true;
                
            }, token);

            onReleased();

        }, ct);
    }

    public async Task<bool> ReserveStockUnsafeAsync(int productId, int qty, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(qty);

        Product? snapshot = await store.GetRecordAsync<Product>(DataFiles.PathFor(DataFiles.Products),
            x => x.Id == productId, ct);

        if (snapshot is null || snapshot.StockQuantity < qty)
            return false;

        await SimulatedLatency.DatabaseWriteAsync("UnsafeProductRepository", async token =>
        {
            await store.UpdateRecordsAsync<Product>(DataFiles.PathFor(DataFiles.Products), products =>
            {
                Product product = products.Single(x => x.Id == productId);

                product.StockQuantity = snapshot.StockQuantity - qty;

                return true;

            }, token);
        }, ct);

        return true;
    }
}
