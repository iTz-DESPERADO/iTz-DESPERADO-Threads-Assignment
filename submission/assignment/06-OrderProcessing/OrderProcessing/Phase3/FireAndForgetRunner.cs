using OrderProcessing.Phase1;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public sealed class FireAndForgetRunner(JsonFileStore store)
{
    public async Task RunAsync(CancellationToken ct)
    {
        TrackingEmailService? tracked = null;

        try
        {
            await new DataSeeder(store).ResetAsync(ct);

            List<Order> input = await new IncomingOrderRepository(store).GetAllAsync(ct);

            CustomerRepository customers = new(store);

            tracked = new(new EmailService(store, new Dictionary<int, int> { [17] = int.MaxValue }), ct);

            BrokenFireAndForgetDemo broken = new(new(store), tracked);

            FixedFireAndForgetDemo fixedDemo = new(new(store), tracked);

            Order first = input.Single(x => x.Id == 1);

            Customer customer = (await customers.GetByIdAsync(first.CustomerId, ct))!;

            Log.Info("BEFORE FIX: successful email is still running when the method returns.");

            await broken.ProcessOrderAsync(first, customer);

            Log.Info($"Broken method returned; email task completed = {tracked.LastTask!.IsCompleted}; " +
                $"sent emails = {(await store.GetAllRecordsAsync<EmailMessage>(DataFiles.PathFor(DataFiles.Emails), ct)).Count}");

            await tracked.LastTask;

            Log.Info("Observer waited: the first email is now sent.");

            Order second = input.Single(x => x.Id == 2);

            customer = (await customers.GetByIdAsync(second.CustomerId, ct))!;

            Log.Info("AFTER FIX: completion is printed after sending the email.");

            await fixedDemo.ProcessOrderAsync(second, customer, ct);

            Log.Info($"Fixed method returned; email task completed = {tracked.LastTask!.IsCompleted}; " +
                $"sent emails = {(await store.GetAllRecordsAsync<EmailMessage>(DataFiles.PathFor(DataFiles.Emails), ct)).Count}");

            Order failure = input.Single(x => x.Id == 17);

            customer = (await customers.GetByIdAsync(failure.CustomerId, ct))!;

            Log.Info("BEFORE FIX: email will throw, but the broken caller reports completion.");

            await broken.ProcessOrderAsync(failure, customer);

            Log.Info("Broken method returned without an email exception.");

            try 
            {
                await tracked.LastTask!;
            }
            catch (SmtpUnavailableException ex)
            {
                Log.Error($"Observer later found email task {tracked.LastTask!.Status}: {ex.Message}");
            }

            Log.Info("AFTER FIX: the caller observes the email exception.");

            try 
            { 
                await fixedDemo.ProcessOrderAsync(failure, customer, ct);
            }
            catch (SmtpUnavailableException ex)
            {
                Log.Error($"Fixed caller caught: {ex.Message}");
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Log.Info("Fire-and-forget investigation cancelled.");
        }
        finally
        {
            if (tracked?.LastTask is { } pending)
            {
                try 
                { 
                    await pending; 
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
                catch (SmtpUnavailableException) { }
            }
        }
    }

    private sealed class TrackingEmailService(IEmailService inner, CancellationToken runToken) : IEmailService
    {
        public Task? LastTask { get; private set; }

        public Task SendConfirmationAsync(Order order, Customer customer, CancellationToken ct = default)
        {
            LastTask = inner.SendConfirmationAsync(order, customer, ct == default ? runToken : ct);

            return LastTask;
        }
    }
}
