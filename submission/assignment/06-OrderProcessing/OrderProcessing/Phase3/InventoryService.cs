using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Simulation;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public sealed class InventoryService(JsonFileStore store)
{
    public bool HasReservation { get; private set; }
    public string? FailureReason { get; private set; }

    public async Task<bool> ReserveStockAsync(Order order, CancellationToken ct)
    {
        bool reserved = false;

        await SimulatedLatency.DatabaseWriteAsync("InventoryService", async token =>
        {
            reserved = await store.UpdateRecordsAsync<Product>(DataFiles.PathFor(DataFiles.Products), products =>
            {
                var items = order.Items.GroupBy(x => x.ProductId).ToList();

                foreach (var item in items)
                {
                    Product? product = products.Find(x => x.Id == item.Key);

                    if (product is null || product.StockQuantity < item.Sum(x => x.Quantity))
                    {
                        FailureReason = product is null ? $"Product #{item.Key} not found"
                            : $"Insufficient stock for '{product.Name}'";

                        return false;
                    }
                }

                foreach (var item in items)
                    products.Single(x => x.Id == item.Key).StockQuantity -= item.Sum(x => x.Quantity);

                return true;

            }, token);

            HasReservation = reserved;

        }, ct);

        return reserved;
    }

    public async Task ReleaseStockAsync(Order order, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!HasReservation)
            return;

        await SimulatedLatency.DatabaseWriteAsync("InventoryService", async token =>
        {
            await store.UpdateRecordsAsync<Product>(DataFiles.PathFor(DataFiles.Products), products =>
            {
                foreach (OrderItem item in order.Items)
                    products.Single(x => x.Id == item.ProductId).StockQuantity += item.Quantity;

                return true;

            }, token);

            HasReservation = false;

        }, ct);
    }
}
