using System.Diagnostics;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Models.Enums;
namespace OrderProcessing.Phase3;

public sealed class SyncOrderProcessor(SyncCustomerService customers, SyncInventoryService inventory,
    SyncPricingService pricing, SyncPaymentGateway payments, SyncOrderRepository orders, SyncEmailService emails)
{
    public bool ProcessOrder(Order order, CancellationToken ct)
    {
        string? transactionId = null;
        try
        {
            try
            {
                Customer customer = Step(order, 1, "Customer", () => customers.GetCustomer(order.CustomerId, ct));

                if (!customer.IsActive) return Finish(order, OrderStatus.Rejected, "Customer is inactive", ct);

                if (!Step(order, 2, "Reserve stock", () => inventory.ReserveStock(order, ct)))
                    return Finish(order, OrderStatus.Rejected, inventory.FailureReason!, ct);

                Step(order, 3, "Pricing", () => pricing.CalculateTotal(order, ct));

                PaymentResult payment = Step(order, 4, "Payment", () => payments.ProcessPayment(order.Id, order.TotalAmount, ct));

                transactionId = payment.TransactionId;

                order.TransactionId = transactionId;

                order.Status = OrderStatus.Completed;

                Step(order, 5, "Save", () => { orders.Save(order, ct); return true; });

                Step(order, 6, "Email", () => { emails.SendConfirmation(order, customer, ct); return true; });

                return true;
            }
            catch (CustomerNotFoundException ex) 
            { 
                return Finish(order, OrderStatus.Rejected, ex.Message, ct); 
            }
            catch (InvalidCouponException ex) 
            { 
                return Finish(order, OrderStatus.Rejected, ex.Message, ct); 
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            using CancellationTokenSource cleanup = new();

            if (transactionId is not null) payments.Refund(transactionId, cleanup.Token);

            return Finish(order, OrderStatus.Pending, "Cancelled", cleanup.Token);
        }
        catch (Exception ex)
        {
            using CancellationTokenSource cleanup = new();

            if (transactionId is not null) payments.Refund(transactionId, cleanup.Token);

            return Finish(order, OrderStatus.Failed, ex.Message, cleanup.Token);
        }
    }

    private bool Finish(Order order, OrderStatus status, string reason, CancellationToken ct)
    {
        inventory.ReleaseStock(order, ct);

        order.Status = status;

        order.FailureReason = reason;

        orders.Save(order, ct);

        return false;
    }

    private static T Step<T>(Order order, int step, string name, Func<T> operation)
    {
        Stopwatch timer = Stopwatch.StartNew();
        try
        {
            T result = operation();
            Log.Info($"Sync order #{order.Id} -> Step {step}/6 {name} completed in {timer.ElapsedMilliseconds} ms");
            return result;
        }
        catch (Exception ex)
        {
            Log.Error($"Sync order #{order.Id} -> Step {step}/6 {name}: {ex.Message} after {timer.ElapsedMilliseconds} ms");
            throw;
        }
    }
}
