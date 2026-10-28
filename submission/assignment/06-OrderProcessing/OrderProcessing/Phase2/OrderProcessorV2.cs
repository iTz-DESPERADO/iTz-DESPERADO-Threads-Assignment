using OrderProcessing.Phase1;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Models.Enums;

namespace OrderProcessing.Phase2;

public sealed class OrderProcessorV2(
    ProductRepository products,
    OrderRepository orders,
    OrderPricer pricer,
    InvoiceGenerator invoiceGenerator)
{
    public List<OrderItem> ReservedItems { get; } = [];

    public async Task<bool> ProcessValidatedOrderAsync(Order order, CancellationToken ct)
    {
        foreach (OrderItem item in order.Items)
        {
            ct.ThrowIfCancellationRequested();

            bool reserved = await products.ReserveStockAsync(item.ProductId, item.Quantity,
                () => ReservedItems.Add(item), ct);

            if (!reserved)
            {
                await ReleaseReservedStockAsync(ct);

                Product? product = await products.GetByIdAsync(item.ProductId, ct);

                order.Status = OrderStatus.Rejected;

                order.FailureReason = $"Insufficient stock for '{product?.Name}'";

                await orders.SaveAsync(order, ct);
                
                return false;
            }
        }
        
        await pricer.PriceOrderAsync(order, ct);

        await invoiceGenerator.GenerateAsync(order, ct);

        order.Status = OrderStatus.Completed;

        await orders.SaveAsync(order, ct);

        return true;
    }

    public async Task ReleaseReservedStockAsync(CancellationToken ct)
    {
        foreach (OrderItem item in ReservedItems.ToArray())
            await products.ReleaseStockAsync(item.ProductId, item.Quantity,
                () => ReservedItems.Remove(item), ct);
    }
}
