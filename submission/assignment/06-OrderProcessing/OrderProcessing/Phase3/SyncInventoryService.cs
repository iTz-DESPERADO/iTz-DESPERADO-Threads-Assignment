using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Simulation;
using OrderProcessing.Shared.Storage;
namespace OrderProcessing.Phase3;

public sealed class SyncInventoryService(JsonFileStore store)
{
    public bool HasReservation { get; private set; }
    public string? FailureReason { get; private set; }

    public bool ReserveStock(Order order, CancellationToken ct)
    {
        SimulatedLatency.DatabaseWrite("SyncInventory", () =>
        {
            HasReservation = store.UpdateRecords<Product>(DataFiles.PathFor(DataFiles.Products), products =>
            {
                foreach (var item in order.Items.GroupBy(x => x.ProductId))
                {
                    Product? product = products.Find(x => x.Id == item.Key);

                    if (product is null || product.StockQuantity < item.Sum(x => x.Quantity))
                    {
                        FailureReason = product is null ? $"Product #{item.Key} not found"
                            : $"Insufficient stock for '{product.Name}'";

                        return false;
                    }
                }

                foreach (OrderItem item in order.Items)
                    products.Single(x => x.Id == item.ProductId).StockQuantity -= item.Quantity;

                return true;
            });
        }, ct);
        return HasReservation;
    }

    public void ReleaseStock(Order order, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!HasReservation) 
            return;

        SimulatedLatency.DatabaseWrite("SyncInventory release", () =>
        {
            store.UpdateRecords<Product>(DataFiles.PathFor(DataFiles.Products), products =>
            {
                foreach (OrderItem item in order.Items)
                    products.Single(x => x.Id == item.ProductId).StockQuantity += item.Quantity;

                return true;
            });

            HasReservation = false;
        }, ct);
    }
}
