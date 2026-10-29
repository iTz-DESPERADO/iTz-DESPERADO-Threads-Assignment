using System.Text.Json;
using OrderProcessing.Phase1;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Models.Enums;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public sealed class ReportGenerator(OrderRepository orders, ProductRepository products)
{
    public async Task<Report> GenerateAsync(CancellationToken ct)
    {
        List<Order> all = await orders.GetAllAsync(ct);

        List<Order> completed = all.Where(x => x.Status == OrderStatus.Completed).ToList();

        var top = completed.SelectMany(x => x.Items)
            .GroupBy(x => x.ProductId)
            .Select(group => new { ProductId = group.Key, Quantity = group.Sum(x => x.Quantity) })
            .OrderByDescending(x => x.Quantity).ThenBy(x => x.ProductId).FirstOrDefault();

        Product? product = top is null ? null : await products.GetByIdAsync(top.ProductId, ct);

        decimal revenue = PricingService.Round(completed.Sum(x => x.TotalAmount));

        Report report = new()
        {
            TotalOrders = all.Count,
            CompletedCount = completed.Count,
            RejectedCount = all.Count(x => x.Status == OrderStatus.Rejected),
            FailedCount = all.Count(x => x.Status == OrderStatus.Failed),
            PaymentFailedCount = all.Count(x => x.Status == OrderStatus.PaymentFailed),
            TotalRevenue = revenue,
            AverageOrderValue = completed.Count == 0 ? 0 : PricingService.Round(revenue / completed.Count),
            TopSellingProduct = product?.Name ?? string.Empty,
            TopSellingQuantity = top?.Quantity ?? 0,
            GeneratedAt = DateTime.Now
        };

        string path = Path.Combine(DataFiles.PathFor(DataFiles.Reports), $"report_{report.GeneratedAt:yyyyMMdd_HHmmss}.json");

        string temporaryPath = path + ".tmp";

        try
        {
            await using (FileStream stream = new(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None,
                4096, FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, report, JsonSettings.Options, ct);
                await stream.FlushAsync(ct);
            }

            ct.ThrowIfCancellationRequested();

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
        
        return report;
    }
}
