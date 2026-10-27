using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Models.Enums;
using OrderProcessing.Shared.Simulation;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public sealed class SyncCouponRepository(JsonFileStore store)
{
    public Coupon? GetByCode(string code, CancellationToken ct)
    {
        return SimulatedLatency.DatabaseQuery("SyncCouponRepository",
            () => store.GetRecord<Coupon>(DataFiles.PathFor(DataFiles.Coupons),
                x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)), ct);
    }
}
