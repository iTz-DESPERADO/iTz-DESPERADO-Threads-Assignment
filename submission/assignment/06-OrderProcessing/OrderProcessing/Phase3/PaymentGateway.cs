using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Models.Enums;
using OrderProcessing.Shared.Simulation;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public sealed class PaymentGateway(JsonFileStore store, IEnumerable<int> timeoutOrderIds,
    IEnumerable<int> declineOrderIds, bool disableFailures = false) : IPaymentGateway
{
    private readonly HashSet<int> _timeouts = disableFailures ? [] : new(timeoutOrderIds);
    private readonly HashSet<int> _declines = disableFailures ? [] : new(declineOrderIds);

    public async Task<PaymentResult> ProcessPaymentAsync(int orderId, decimal amount, CancellationToken ct)
    {
        string actor = $"Payment #{orderId}";

        await SimulatedLatency.StepAsync(actor, "Authorizing", 800, ct);

        await SimulatedLatency.StepAsync(actor, "Fraud check", 500, ct);

        bool declined = _declines.Contains(orderId);

        if (!declined)
            await SimulatedLatency.StepAsync(actor, "Capturing", _timeouts.Contains(orderId) ? 5000 : 700, ct);

        string? transactionId = declined ? null : "TXN-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

        PaymentStatus status = declined ? PaymentStatus.Declined : PaymentStatus.Captured;

        await store.UpdateRecordsAsync<PaymentTransaction>(DataFiles.PathFor(DataFiles.Payments), payments =>
        {
            payments.Add(new PaymentTransaction
            {
                OrderId = orderId, 
                Amount = amount,
                Status = status,
                TransactionId = transactionId, 
                CreatedAt = DateTime.Now
            });

            return true;

        }, ct);

        return new PaymentResult
        {
            Success = !declined, 
            Status = status, 
            TransactionId = transactionId,
            Message = declined ? "Card declined by issuer" : null
        };
    }

    public async Task RefundAsync(string transactionId, CancellationToken ct)
    {
        await SimulatedLatency.ExternalApiAsync("Payment refund", ct);

        await store.UpdateRecordsAsync<PaymentTransaction>(DataFiles.PathFor(DataFiles.Payments), payments =>
        {
            PaymentTransaction payment = payments.Single(x => x.TransactionId == transactionId);

            payment.Status = PaymentStatus.Refunded;

            return true;

        }, ct);
    }
}
