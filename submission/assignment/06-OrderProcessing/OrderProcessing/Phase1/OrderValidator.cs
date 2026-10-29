using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Models.Enums;
using OrderProcessing.Shared.Simulation;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public sealed class OrderValidator(
    CustomerRepository customers,
    ProductRepository products,
    CouponRepository coupons)
{
    public async Task ValidateOrderAsync(Order order, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        order.FailureReason = null;

        Customer? customer = await customers.GetByIdAsync(order.CustomerId, ct);

        if (customer is null)
        {
            order.Reject($"Customer #{order.CustomerId} not found");
            return;
        }

        if (!customer.IsActive)
        {
            order.Reject("Customer is inactive");
            return;
        }

        List<(OrderItem Item, Product Product)> found = [];

        foreach (OrderItem item in order.Items)
        {
            Product? product = await products.GetByIdAsync(item.ProductId, ct);

            if (product is null)
            {
                order.Reject($"Product #{item.ProductId} not found");
                return;
            }

            found.Add((item, product));
        }

        foreach (var entry in found)
        {
            ct.ThrowIfCancellationRequested();

            if (entry.Product.StockQuantity < entry.Item.Quantity)
            {
                order.Reject($"Insufficient stock for '{entry.Product.Name}'");
                return;
            }
        }

        if (order.CouponCode is not null)
        {
            Coupon? coupon = await coupons.GetByCodeAsync(order.CouponCode, ct);

            if (coupon is null)
            {
                order.Reject($"Coupon '{order.CouponCode}' does not exist");
                return;
            }
            if (coupon.ExpiryDate < DateTime.Today)
            {
                order.Reject($"Coupon '{order.CouponCode}' has expired");
                return;
            }
        }

        ct.ThrowIfCancellationRequested();
        
        order.Status = OrderStatus.Validated;
    }
}
