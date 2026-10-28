using OrderProcessing.Phase1;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Models.Enums;
using OrderProcessing.Shared.Simulation;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public sealed class PricingService(
    JsonFileStore store,
    CustomerRepository customers,
    CouponRepository coupons,
    IEnumerable<int> failShippingForOrderIds)
{
    private readonly HashSet<int> _failShippingForOrderIds = new(failShippingForOrderIds);

    public async Task<decimal> CalculateSubTotalAsync(Order order, CancellationToken ct)
    {
        List<Product> products = await SimulatedLatency.DatabaseQueryAsync("PricingService",
            token => store.GetAllRecordsAsync<Product>(DataFiles.PathFor(DataFiles.Products), token), ct);

        foreach (OrderItem item in order.Items)
        {
            ct.ThrowIfCancellationRequested();
            item.UnitPrice = Round(products.Single(x => x.Id == item.ProductId).Price);
        }

        return Round(order.Items.Sum(x => x.UnitPrice * x.Quantity));
    }

    public async Task<decimal> GetDiscountAsync(Order order, CancellationToken ct)
    {
        await SimulatedLatency.ExternalApiAsync("Discount API", ct);

        Customer customer = await customers.GetByIdAsync(order.CustomerId, ct)
            ?? throw new CustomerNotFoundException($"Customer #{order.CustomerId} not found");

        int percent = customer.LoyaltyTier switch
        {
            LoyaltyTier.Gold => 10,
            LoyaltyTier.Silver => 5,
            _ => 0
        };

        if (order.CouponCode is not null)
        {
            Coupon coupon = await coupons.GetByCodeAsync(order.CouponCode, ct)
                ?? throw new InvalidCouponException($"Coupon '{order.CouponCode}' does not exist");

            if (coupon.ExpiryDate < DateTime.Today)
                throw new InvalidCouponException($"Coupon '{order.CouponCode}' has expired");

            percent += coupon.DiscountPercent;
        }

        return Round(order.SubTotal * Math.Min(percent, 30) / 100m);
    }

    public async Task<decimal> CalculateShippingCostAsync(Order order, CancellationToken ct)
    {
        await SimulatedLatency.StepAsync("Shipping API", "DNS/handshake", 150, ct);

        if (_failShippingForOrderIds.Contains(order.Id))
            throw new ShippingProviderUnavailableException("Shipping provider unavailable");

        await SimulatedLatency.StepAsync("Shipping API", "Sending request", 300, ct);

        await SimulatedLatency.StepAsync("Shipping API", "Waiting for response", Random.Shared.Next(500, 1501), ct);

        List<Product> products = await store.GetAllRecordsAsync<Product>(DataFiles.PathFor(DataFiles.Products), ct);

        decimal weight = order.Items.Sum(item => products.Single(p => p.Id == item.ProductId).WeightKg * item.Quantity);

        return order.SubTotal >= 1000m ? 0m : Round(5m + 1.5m * order.Items.Sum(x => x.Quantity) + 2m * weight);
    }

    internal static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
