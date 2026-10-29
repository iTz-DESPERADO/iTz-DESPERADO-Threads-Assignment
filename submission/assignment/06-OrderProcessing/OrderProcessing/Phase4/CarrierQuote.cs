using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Simulation;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase4;

internal static class CarrierQuote
{
    public static async Task<decimal> GetAsync(JsonFileStore store, string name, Order order,
        int minimum, int maximum, decimal factor, bool injectedFailure, bool builtInFailure, CancellationToken ct)
    {
        try
        {
            await SimulatedLatency.StepAsync(name, "Handshake", 150, ct);

            if (injectedFailure) 
                throw new ShippingProviderUnavailableException($"{name} unavailable");

            await SimulatedLatency.StepAsync(name, "Waiting for quote", Random.Shared.Next(minimum, maximum + 1), ct);

            if (builtInFailure) 
            throw new ShippingProviderUnavailableException($"{name} unavailable");

            List<Product> products = await store.GetAllRecordsAsync<Product>(DataFiles.PathFor(DataFiles.Products), ct);

            decimal subtotal = order.Items.Sum(x => products.Single(p => p.Id == x.ProductId).Price * x.Quantity);

            decimal weight = order.Items.Sum(x => products.Single(p => p.Id == x.ProductId).WeightKg * x.Quantity);

            decimal price = subtotal >= 1000m ? 0m : 5m + 1.5m * order.Items.Sum(x => x.Quantity) + 2m * weight;

            return Math.Round(price * factor, 2, MidpointRounding.AwayFromZero);

        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Log.Info($"[{name}] cancelled");
            throw;
        }
    }
}
