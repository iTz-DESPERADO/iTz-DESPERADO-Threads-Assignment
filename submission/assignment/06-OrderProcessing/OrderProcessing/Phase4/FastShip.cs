using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;
namespace OrderProcessing.Phase4;

public sealed class FastShip(JsonFileStore store, IEnumerable<int> failForOrderIds) : IShippingCarrier
{
    private readonly HashSet<int> _failures = new(failForOrderIds);

    public string Name => "FastShip";
    
    public async Task<decimal> GetQuoteAsync(Order order, CancellationToken ct)
    {
        return await CarrierQuote.GetAsync(store, Name, order, 500, 1500, 1.00m,
            _failures.Contains(order.Id), false, ct);
    }
}
