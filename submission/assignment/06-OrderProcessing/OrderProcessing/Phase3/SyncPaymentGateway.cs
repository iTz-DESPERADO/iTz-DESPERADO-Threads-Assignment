using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Models.Enums;
using OrderProcessing.Shared.Simulation;
using OrderProcessing.Shared.Storage;
namespace OrderProcessing.Phase3;

public sealed class SyncPaymentGateway(JsonFileStore store)
{
    public PaymentResult ProcessPayment(int orderId, decimal amount, CancellationToken ct)
    {
        SimulatedLatency.PaymentGateway($"SyncPayment #{orderId}", ct);

        string transactionId = "TXN-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

        store.UpdateRecords<PaymentTransaction>(DataFiles.PathFor(DataFiles.Payments), payments =>
        {
            payments.Add(new PaymentTransaction { OrderId = orderId, Amount = amount,
                Status = PaymentStatus.Captured, TransactionId = transactionId, CreatedAt = DateTime.Now });
            return true;
        });

        return new PaymentResult { Success = true, Status = PaymentStatus.Captured, TransactionId = transactionId };
    }

    public void Refund(string transactionId, CancellationToken ct)
    {
        SimulatedLatency.ExternalApi("SyncRefund", ct);

        store.UpdateRecords<PaymentTransaction>(DataFiles.PathFor(DataFiles.Payments), payments =>
        {
            payments.Single(x => x.TransactionId == transactionId).Status = PaymentStatus.Refunded;
            return true;
        });
    }
}
