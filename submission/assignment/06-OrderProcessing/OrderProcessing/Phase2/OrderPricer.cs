using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase2;

public sealed class OrderPricer(PricingService pricing)
{
    public async Task PriceOrderAsync(Order order, CancellationToken ct)
    {
        order.SubTotal = await pricing.CalculateSubTotalAsync(order, ct);

        Task<decimal> discount = pricing.GetDiscountAsync(order, ct);

        Task<decimal> shipping = pricing.CalculateShippingCostAsync(order, ct);
        try
        {
            order.Discount = await discount;
        }
        finally
        {
            order.ShippingCost = await shipping;
        }
        order.TotalAmount = PricingService.Round(order.SubTotal - order.Discount + order.ShippingCost);
    }

    public async Task PriceOrderSequentialAsync(Order order, CancellationToken ct)
    {
        order.SubTotal = await pricing.CalculateSubTotalAsync(order, ct);

        order.Discount = await pricing.GetDiscountAsync(order, ct);

        order.ShippingCost = await pricing.CalculateShippingCostAsync(order, ct);
        
        order.TotalAmount = PricingService.Round(order.SubTotal - order.Discount + order.ShippingCost);
    }
}
