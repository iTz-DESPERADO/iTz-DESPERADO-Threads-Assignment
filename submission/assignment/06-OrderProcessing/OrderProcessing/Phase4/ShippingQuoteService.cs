using System.Collections.Concurrent;
using System.Diagnostics;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase4;

public sealed class ShippingQuoteService(IEnumerable<IShippingCarrier> carriers)
{
    private readonly IShippingCarrier[] _carriers = carriers.ToArray();
    public IReadOnlyCollection<string> CancelledCarriers { get; private set; } = [];

    public async Task<ShippingQuote> GetFastestQuoteAsync(Order order, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        Stopwatch timer = Stopwatch.StartNew();

        using CancellationTokenSource budget = CancellationTokenSource.CreateLinkedTokenSource(ct);

        budget.CancelAfter(1200);

        ConcurrentBag<string> cancelled = [];

        Task<ShippingQuote>[] tasks = _carriers.Select(carrier => RequestAsync(carrier, order, timer, cancelled, budget.Token)).ToArray();

        List<Task<ShippingQuote>> pending = tasks.ToList();

        try
        {
            while (pending.Count > 0)
            {
                Task<ShippingQuote> next = await Task.WhenAny(pending);

                pending.Remove(next);

                try
                {
                    ShippingQuote quote = await next;

                    ct.ThrowIfCancellationRequested();

                    if (quote.ElapsedMs <= 1200) 
                    return quote;
                }
                catch (ShippingProviderUnavailableException ex) 
                { 
                    Log.Error(ex.Message);
                }
                catch (OperationCanceledException) when (budget.IsCancellationRequested) { }

                ct.ThrowIfCancellationRequested();
            }

            if (budget.IsCancellationRequested || timer.ElapsedMilliseconds >= 1200)
                return new ShippingQuote { Carrier = "Flat rate", Price = 25m, ElapsedMs = timer.ElapsedMilliseconds };

            throw new ShippingProviderUnavailableException("All carriers failed");
        }
        finally
        {
            budget.Cancel();

            try 
            { 
                await Task.WhenAll(tasks);
            }
            catch (Exception) when (tasks.All(x => x.IsCompleted)) { }

            CancelledCarriers = cancelled.Order().ToArray();
        }
    }

    private static async Task<ShippingQuote> RequestAsync(IShippingCarrier carrier, Order order, Stopwatch timer,
        ConcurrentBag<string> cancelled, CancellationToken ct)
    {
        try
        {
            decimal price = await carrier.GetQuoteAsync(order, ct);

            return new ShippingQuote { Carrier = carrier.Name, Price = price, ElapsedMs = timer.ElapsedMilliseconds };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            cancelled.Add(carrier.Name);
            
            throw;
        }
    }
}
