using System.Diagnostics;
using OrderProcessing.Phase1;
using OrderProcessing.Phase2;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Models.Enums;

namespace OrderProcessing.Phase3;

public sealed class OrderProcessor(CustomerService customers, InventoryService inventory,
    PricingService pricing, IPaymentGateway payments, OrderRepository orders, EmailRetryService emails)
{
    public async Task<bool> ProcessOrderAsync(Order order, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        string? capturedTransactionId = null;
        try
        {
            try
            {
                Customer customer = await StepAsync(order, 1, "Customer",
                    token => customers.GetCustomerAsync(order.CustomerId, token), ct);

                if (!customer.IsActive)
                    return await FinishAsync(order, OrderStatus.Rejected, "Customer is inactive", ct);

                bool reserved = await StepAsync(order, 2, "Reserve stock",
                    token => inventory.ReserveStockAsync(order, token), ct);

                if (!reserved)
                    return await FinishAsync(order, OrderStatus.Rejected, inventory.FailureReason!, ct);

                await StepAsync(order, 3, "Pricing", token => pricing.CalculateTotalAsync(order, token), ct);

                PaymentResult payment;

                using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    timeout.CancelAfter(3000);
                    Stopwatch paymentTimer = Stopwatch.StartNew();
                    try
                    {
                        payment = await StepAsync(order, 4, "Payment",
                            token => payments.ProcessPaymentAsync(order.Id, order.TotalAmount, token), timeout.Token);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested && timeout.IsCancellationRequested)
                    {
                        Log.Error($"Order #{order.Id} -> Step 4/6 Payment ... TIMEOUT after {paymentTimer.ElapsedMilliseconds} ms");
                        return await FinishAsync(order, OrderStatus.PaymentFailed, "Payment gateway timeout (3s)", ct);
                    }
                }
                if (!payment.Success)
                    return await FinishAsync(order, OrderStatus.PaymentFailed, payment.Message!, ct);

                capturedTransactionId = payment.TransactionId;

                order.TransactionId = payment.TransactionId;

                order.Status = OrderStatus.Completed;

                try
                {
                    await StepAsync(order, 5, "Save", token => orders.SaveAsync(order, token), ct);
                }
                catch (DatabaseWriteException)
                {
                    await payments.RefundAsync(capturedTransactionId!, ct);

                    capturedTransactionId = null;

                    return await FinishAsync(order, OrderStatus.Failed,
                        $"Database save failed after payment; payment {order.TransactionId} refunded", ct);
                }
                await StepAsync(order, 6, "Email", token => emails.SendConfirmationWithRetryAsync(order, customer, token), ct);

                return order.Status == OrderStatus.Completed;
            }
            catch (CustomerNotFoundException ex)
            {
                return await FinishAsync(order, OrderStatus.Rejected, ex.Message, ct);
            }
            catch (InvalidCouponException ex)
            {
                return await FinishAsync(order, OrderStatus.Rejected, ex.Message, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            using CancellationTokenSource cleanup = new();

            if (capturedTransactionId is not null)
                await payments.RefundAsync(capturedTransactionId, cleanup.Token);

            return await FinishAsync(order, OrderStatus.Pending, "Cancelled", cleanup.Token);
        }
        catch (Exception ex)
        {
            using CancellationTokenSource cleanup = new();

            if (capturedTransactionId is not null)
                await payments.RefundAsync(capturedTransactionId, cleanup.Token);

            return await FinishAsync(order, OrderStatus.Failed, ex.Message, cleanup.Token);
        }
    }

    private async Task<bool> FinishAsync(Order order, OrderStatus status, string reason, CancellationToken ct)
    {
        await inventory.ReleaseStockAsync(order, ct);

        order.Status = status;

        order.FailureReason = reason;

        await orders.SaveAsync(order, ct);

        Log.Info($"Order #{order.Id} -> {status}: {reason}");

        return false;

    }

    private static async Task<T> StepAsync<T>(Order order, int step, string name,
        Func<CancellationToken, Task<T>> operation, CancellationToken ct)
    {
        Stopwatch timer = Stopwatch.StartNew();

        Log.Info($"Order #{order.Id} -> Step {step}/6 {name} started");

        try
        {
            T result = await operation(ct);

            string outcome = result switch
            {
                PaymentResult payment => payment.Success ? $"OK ({payment.TransactionId})" : $"DECLINED ({payment.Message})",
                bool value => value ? "OK" : "REJECTED",
                _ => "OK"
            };

            Log.Success($"Order #{order.Id} -> Step {step}/6 {name} ... {outcome} in {timer.ElapsedMilliseconds} ms");

            return result;
        }
        catch (Exception ex)
        {
            Log.Error($"Order #{order.Id} -> Step {step}/6 {name} ... {ex.Message} after {timer.ElapsedMilliseconds} ms");

            throw;
        }
    }

    private static async Task StepAsync(Order order, int step, string name,
        Func<CancellationToken, Task> operation, CancellationToken ct)
    {
        await StepAsync(order, step, name, async token =>
        {
            await operation(token);
            return true;
        }, ct);
    }
}
