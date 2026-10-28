using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Simulation;
using OrderProcessing.Shared.Storage;
namespace OrderProcessing.Phase3;

public sealed class SyncEmailService(JsonFileStore store)
{
    public void SendConfirmation(Order order, Customer customer, CancellationToken ct)
    {
        SimulatedLatency.Email($"SyncEmail #{order.Id}", ct);

        EmailMessage message = EmailService.CreateMessage(order, customer, 1);

        message.SentAt = DateTime.Now;

        store.UpdateRecords<EmailMessage>(DataFiles.PathFor(DataFiles.Emails), emails =>
        {
            emails.Add(message);

            return true;
        });
    }
}
