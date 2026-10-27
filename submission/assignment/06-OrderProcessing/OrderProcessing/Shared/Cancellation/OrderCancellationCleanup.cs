using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Models.Enums;
using OrderProcessing.Shared.Simulation;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Shared.Cancellation;

public sealed class OrderCancellationCleanup(JsonFileStore store)
{
    public async Task CancelOrderAsync(
        Order order,
        IReadOnlyCollection<OrderItem> actuallyReservedItems,
        string? capturedTransactionId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);

        ArgumentNullException.ThrowIfNull(actuallyReservedItems);

        if (actuallyReservedItems.Count > 0)
        {
            await SimulatedLatency.DatabaseWriteAsync("InventoryCancellation", async stepCt =>
            {
                await store.UpdateRecordsAsync<Product>(DataFiles.PathFor(DataFiles.Products), products =>
                {
                    foreach (IGrouping<int, OrderItem> group in actuallyReservedItems.GroupBy(x => x.ProductId))
                    {
                        Product product = products.Find(p => p.Id == group.Key)
                            ?? throw new InvalidOperationException($"Product #{group.Key} missing during release.");

                        product.StockQuantity += group.Sum(i => i.Quantity);
                    }

                    return true;

                }, stepCt);
            }, ct);

            Log.Info($"Order #{order.Id} -> Reserved stock released.");
        }

        if (!string.IsNullOrWhiteSpace(capturedTransactionId))
        {
            await SimulatedLatency.ExternalApiAsync("PaymentRefund", ct);

            bool updated = await store.UpdateRecordsAsync<PaymentTransaction>(
                    DataFiles.PathFor(DataFiles.Payments), payments =>
                    {
                        PaymentTransaction payment = payments.Find(p => p.TransactionId == capturedTransactionId)
                            ?? throw new InvalidOperationException("Captured transaction not found.");

                        if (payment.Status != PaymentStatus.Captured)
                            throw new InvalidOperationException("Payment must be captured before refund.");

                        payment.Status = PaymentStatus.Refunded;

                        return true;

                    }, ct);

            if (!updated) 
                throw new InvalidOperationException("Refund update was not saved.");

            Log.Info($"Order #{order.Id} -> Captured payment refunded.");
        }

        order.Status = OrderStatus.Pending;

        order.FailureReason = "Cancelled";

        await SimulatedLatency.DatabaseWriteAsync("OrderCancellation", async stepCt =>
        {
            await store.UpdateRecordsAsync<Order>(DataFiles.PathFor(DataFiles.Orders), orders =>
            {
                int index = orders.FindIndex(o => o.Id == order.Id);

                if (index < 0) 
                    orders.Add(order);

                else 
                    orders[index] = order;

                return true;

            }, stepCt);
        }, ct);

        Log.Success($"Order #{order.Id} -> Pending (Cancelled).");
    }
}
