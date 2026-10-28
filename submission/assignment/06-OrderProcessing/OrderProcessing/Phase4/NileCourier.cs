using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;
namespace OrderProcessing.Phase4;

public sealed class NileCourier(JsonFileStore store, IEnumerable<int> failForOrderIds) : IShippingCarrier
{
    private readonly HashSet<int> _failures = new(failForOrderIds);

    public string Name => "NileCourier";
    
    public async Task<decimal> GetQuoteAsync(Order order, CancellationToken ct)
    {
        return await CarrierQuote.GetAsync(store, Name, order, 800, 1200, 1.10m,
            _failures.Contains(order.Id), false, ct);
    }
}
