using OrderProcessing.Phase1;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Simulation;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public sealed class RaceConditionDemo(JsonFileStore store, ProductRepository products)
{
    public async Task RunAsync(CancellationToken ct)
    {
        try
        {
            await SetKeyboardStockAsync(ct);

            Task<bool> first = products.ReserveStockUnsafeAsync(4, 1, ct);

            Task<bool> second = products.ReserveStockUnsafeAsync(4, 1, ct);

            bool firstResult;

            bool secondResult;

            try 
            { 
              firstResult = await first; 
            }
            finally 
            { 
              secondResult = await second;
            }

            Product? keyboard = await products.GetByIdAsync(4, ct);

            Log.Info($"Unsafe: Order #10 = {firstResult}, Order #11 = {secondResult}, stock = {keyboard?.StockQuantity}");

            await SetKeyboardStockAsync(ct);

            first = products.ReserveStockAsync(4, 1, ct);

            second = products.ReserveStockAsync(4, 1, ct);

            try 
            { 
              firstResult = await first;
            }
            finally 
            { 
              secondResult = await second;
            }

            keyboard = await products.GetByIdAsync(4, ct);

            Log.Info($"Safe: Order #10 = {firstResult}, Order #11 = {secondResult}, stock = {keyboard?.StockQuantity}");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            using CancellationTokenSource cleanup = new();

            await SetKeyboardStockAsync(cleanup.Token);

            throw;
        }
    }

    private async Task SetKeyboardStockAsync(CancellationToken ct)
    {
        await SimulatedLatency.DatabaseWriteAsync("RaceConditionDemo", async token =>
        {
            await store.UpdateRecordsAsync<Product>(DataFiles.PathFor(DataFiles.Products), products =>
            {
                products.Single(x => x.Id == 4).StockQuantity = 1;

                return true;
                
            }, token);
        }, ct);
    }
}
