using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public sealed class EmailRetryService(IEmailService emails, JsonFileStore store)
{
    public async Task SendConfirmationWithRetryAsync(Order order, Customer customer, CancellationToken ct)
    {
        int[] delays = [500, 1000, 2000];
        for (int attempt = 1; attempt <= 4; attempt++)
        {
            try
            {
                await emails.SendConfirmationAsync(order, customer, ct);
                return;
            }
            catch (SmtpUnavailableException ex)
            {
                Log.Error($"Email for Order #{order.Id}, attempt {attempt} failed: {ex.Message}");
                if (attempt == 4)
                {
                    EmailMessage message = EmailService.CreateMessage(order, customer, attempt);

                    message.LastError = ex.Message;

                    await store.UpdateRecordsAsync<EmailMessage>(DataFiles.PathFor(DataFiles.EmailRetry), queue =>
                    {
                        queue.Add(message);
                        return true;
                    }, ct);

                    Log.Info($"Email for Order #{order.Id} queued for retry later");

                    return;
                }
                await Task.Delay(delays[attempt - 1], ct);
            }
        }
    }
}
