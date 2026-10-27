using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Models.Enums;
using OrderProcessing.Shared.Simulation;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public sealed class CouponRepository(JsonFileStore store)
{
    public async Task<Coupon?> GetByCodeAsync(string code, CancellationToken ct)
    {
        return await SimulatedLatency.DatabaseQueryAsync("CouponRepository",
            token => store.GetRecordAsync<Coupon>(DataFiles.PathFor(DataFiles.Coupons),
                x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase), token), ct);
    }
}
