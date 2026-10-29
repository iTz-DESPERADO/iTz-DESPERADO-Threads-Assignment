using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;
namespace OrderProcessing.Phase4;

public sealed class CairoExpress(JsonFileStore store, IEnumerable<int> failForOrderIds) : IShippingCarrier
{
    private readonly HashSet<int> _failures = new(failForOrderIds);

    public string Name => "CairoExpress";
    
    public async Task<decimal> GetQuoteAsync(Order order, CancellationToken ct)
    {
        return await CarrierQuote.GetAsync(store, Name, order, 300, 2000, 0.95m,
            _failures.Contains(order.Id), order.Id % 2 == 0, ct);
    }
}
