using OrderProcessing.Phase1;
using OrderProcessing.Phase2;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Models.Enums;
using OrderProcessing.Shared.Simulation;
using OrderProcessing.Shared.Storage;
namespace OrderProcessing.Phase3;

public sealed class SyncPricingService(JsonFileStore store, SyncCustomerRepository customers, SyncCouponRepository coupons)
{
    public decimal CalculateTotal(Order order, CancellationToken ct)
    {
        List<Product> products = SimulatedLatency.DatabaseQuery("SyncPricing",
            () => store.GetAllRecords<Product>(DataFiles.PathFor(DataFiles.Products)), ct);

        foreach (OrderItem item in order.Items)
            item.UnitPrice = PricingService.Round(products.Single(x => x.Id == item.ProductId).Price);

        order.SubTotal = PricingService.Round(order.Items.Sum(x => x.UnitPrice * x.Quantity));

        SimulatedLatency.ExternalApi("SyncDiscount API", ct);

        Customer customer = customers.GetById(order.CustomerId, ct)
            ?? throw new CustomerNotFoundException($"Customer #{order.CustomerId} not found");

        int percent = customer.LoyaltyTier switch { LoyaltyTier.Gold => 10, LoyaltyTier.Silver => 5, _ => 0 };

        if (order.CouponCode is not null)
        {
            Coupon coupon = coupons.GetByCode(order.CouponCode, ct)
                ?? throw new InvalidCouponException($"Coupon '{order.CouponCode}' does not exist");

            if (coupon.ExpiryDate < DateTime.Today)
                throw new InvalidCouponException($"Coupon '{order.CouponCode}' has expired");

            percent += coupon.DiscountPercent;
        }

        order.Discount = PricingService.Round(order.SubTotal * Math.Min(percent, 30) / 100m);

        SimulatedLatency.ExternalApi("SyncShipping API", ct);

        products = store.GetAllRecords<Product>(DataFiles.PathFor(DataFiles.Products));

        decimal weight = order.Items.Sum(x => products.Single(p => p.Id == x.ProductId).WeightKg * x.Quantity);

        order.ShippingCost = order.SubTotal >= 1000 ? 0 : PricingService.Round(5m + 1.5m * order.Items.Sum(x => x.Quantity) + 2m * weight);

        order.TotalAmount = PricingService.Round(order.SubTotal - order.Discount + order.ShippingCost);

        return order.TotalAmount;
    }
}
