using System.Text;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public sealed class InvoiceGenerator
{
    public async Task GenerateAsync(Order order, CancellationToken ct)
    {
        Log.Info($"Invoice #{order.Id} -> Rendering...");

        long checksum = await Task.Run(() =>
        {
            long value = 0;
            for (int i = 0; i < 20_000_000; i++)
            {
                if (i % 4096 == 0)
                    ct.ThrowIfCancellationRequested();

                value += i % 97;
            }
            ct.ThrowIfCancellationRequested();

            return value;
        }, ct);

        string content = FormattableString.Invariant($"""
            Invoice #{order.Id}
            Customer #{order.CustomerId}
            SubTotal: {order.SubTotal:F2}
            Discount: {order.Discount:F2}
            Shipping: {order.ShippingCost:F2}
            Total: {order.TotalAmount:F2}
            Render checksum: {checksum}
            """);

        Log.Info($"Invoice #{order.Id} -> Writing file...");
        
        await File.WriteAllTextAsync(Path.Combine(DataFiles.PathFor(DataFiles.Invoices), $"invoice_{order.Id}.txt"),
            content, Encoding.UTF8, ct);
    }
}
