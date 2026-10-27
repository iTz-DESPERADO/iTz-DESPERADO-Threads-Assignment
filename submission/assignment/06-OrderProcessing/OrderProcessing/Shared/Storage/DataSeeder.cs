
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Models.Enums;
using System.Net;

namespace OrderProcessing.Shared.Storage;

public sealed class DataSeeder(JsonFileStore store)
{
    public async Task ResetAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (Directory.Exists(DataFiles.Root))
            Directory.Delete(DataFiles.Root, recursive: true);

        Directory.CreateDirectory(DataFiles.Root);

        Directory.CreateDirectory(DataFiles.PathFor(DataFiles.Invoices));

        Directory.CreateDirectory(DataFiles.PathFor(DataFiles.Reports));

        Customer[] customers =
        [
            Customer(1, "Ahmed Ali", true, LoyaltyTier.Gold),
        Customer(2, "Sara Mohamed", true, LoyaltyTier.Silver),
        Customer(3, "Omar Hassan", true, LoyaltyTier.Bronze),
        Customer(4, "Mona Ibrahim", false, LoyaltyTier.Silver),
        Customer(5, "Youssef Adel", true, LoyaltyTier.Gold),
        Customer(6, "Nour Khaled", true, LoyaltyTier.Bronze),
        Customer(7, "Karim Samir", true, LoyaltyTier.Silver),
        Customer(8, "Laila Mahmoud", true, LoyaltyTier.Gold),
        Customer(9, "Hassan Tarek", true, LoyaltyTier.Bronze),
        Customer(10, "Dina Fathy", true, LoyaltyTier.Silver)
        ];

        Product[] products =
        [
            Product(1, "Laptop", 1200.00m, 10, 2.2m),
        Product(2, "Smartphone", 800.00m, 15, 0.4m),
        Product(3, "Wireless Mouse", 25.00m, 2, 0.1m),
        Product(4, "Mechanical Keyboard", 90.00m, 1, 0.9m),
        Product(5, "USB-C Hub", 45.00m, 30, 0.2m),
        Product(6, "Headphones", 150.00m, 12, 0.3m),
        Product(7, "27\" Monitor", 350.00m, 6, 5.0m),
        Product(8, "Webcam", 70.00m, 20, 0.2m)
        ];

        DateTime today = DateTime.Today;
        Coupon[] coupons =
        [
            new() { Code = "SAVE10", DiscountPercent = 10, ExpiryDate = today.AddDays(180) },
        new() { Code = "WELCOME15", DiscountPercent = 15, ExpiryDate = today.AddDays(90) },
        new() { Code = "FLASH20", DiscountPercent = 20, ExpiryDate = today.AddDays(7) },
        new() { Code = "OLD20", DiscountPercent = 20, ExpiryDate = today.AddDays(-30) }
        ];

        Order[] incoming =
        [
        Order(1, 1, "SAVE10", (1, 1), (3, 1)),
        Order(2, 2, null, (2, 1)),
        Order(3, 3, "WELCOME15", (5, 2), (8, 1)),
        Order(4, 4, null, (6, 1)),
        Order(5, 5, null, (7, 1)),
        Order(6, 6, "FLASH20", (5, 1)),
        Order(7, 7, null, (6, 1), (8, 1)),
        Order(8, 8, "SAVE10", (1, 1)),
        Order(9, 9, null, (3, 5)),
        Order(10, 10, null, (4, 1)),
        Order(11, 1, null, (4, 1)),
        Order(12, 2, "OLD20", (2, 1)),
        Order(13, 3, null, (6, 2)),
        Order(14, 5, "WELCOME15", (8, 2), (5, 1)),
        Order(15, 99, null, (2, 1)),
        Order(16, 6, null, (7, 1), (5, 1)),
        Order(17, 7, "SAVE10", (2, 1)),
        Order(18, 8, "FAKE50", (6, 1)),
        Order(19, 9, null, (8, 1)),
        Order(20, 10, "FLASH20", (1, 1), (6, 1))
        ];

        List<Task> tasks = new(8)
                {
                    SeedAsync(DataFiles.Customers, customers, ct),
                    SeedAsync(DataFiles.Products, products, ct),
                    SeedAsync(DataFiles.Coupons, coupons, ct),
                    SeedAsync(DataFiles.IncomingOrders, incoming, ct),
                    SeedAsync(DataFiles.Orders, Array.Empty<Order>(), ct),
                    SeedAsync(DataFiles.Payments, Array.Empty<PaymentTransaction>(), ct),
                    SeedAsync(DataFiles.Emails, Array.Empty<EmailMessage>(), ct),
                    SeedAsync(DataFiles.EmailRetry, Array.Empty<EmailMessage>(), ct)
                };

        foreach (Task task in tasks)
        {
            await task;
        }

    }

    private async Task SeedAsync<T>(
    string file,
    IReadOnlyCollection<T> records,
    CancellationToken ct)
    {
        await store.SeedAsync(
            DataFiles.PathFor(file),
            records,
            ct);

        Log.Info($"[Seeder] {file} -> {records.Count} records");
    }

    private static Customer Customer(int id, string name, bool active, LoyaltyTier tier)
    {
        string[] words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return new Customer
        {
            Id = id,
            Name = name,
            Email = $"{words[0].ToLowerInvariant()}.{words[1].ToLowerInvariant()}@example.com",
            IsActive = active,
            LoyaltyTier = tier
        };
    }

    private static Product Product(int id, string name, decimal price, int stock, decimal weight) =>
        new() { Id = id, Name = name, Price = price, StockQuantity = stock, WeightKg = weight };

    private static Order Order(int id, int customerId, string? coupon, params (int ProductId, int Quantity)[] items) =>
        new()
        {
            Id = id,
            CustomerId = customerId,
            CouponCode = coupon,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.Now,
            Items = items.Select(i => new OrderItem
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitPrice = 0m
            }).ToList()
        };
}
