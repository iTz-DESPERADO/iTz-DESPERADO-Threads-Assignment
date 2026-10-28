using System.Collections.Concurrent;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Simulation;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public sealed class EmailService(JsonFileStore store, IReadOnlyDictionary<int, int> failAttempts,
    bool disableFailures = false) : IEmailService
{
    private readonly Dictionary<int, int> _failAttempts = disableFailures ? [] : new(failAttempts);
    private readonly ConcurrentDictionary<int, int> _attempts = new();

    public async Task SendConfirmationAsync(Order order, Customer customer, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        int attempt = _attempts.AddOrUpdate(order.Id, 1, (_, previous) => previous + 1);

        await SimulatedLatency.EmailAsync($"Email #{order.Id}, attempt {attempt}", ct);

        if (_failAttempts.TryGetValue(order.Id, out int failures) && attempt <= failures)
            throw new SmtpUnavailableException("SMTP server unavailable");

        EmailMessage message = CreateMessage(order, customer, attempt);

        message.SentAt = DateTime.Now;

        await store.UpdateRecordsAsync<EmailMessage>(DataFiles.PathFor(DataFiles.Emails), emails =>
        {
            emails.Add(message);
            return true;
        }, ct);

    }

    internal static EmailMessage CreateMessage(Order order, Customer customer, int attempts) => new()
    {
        Id = Guid.NewGuid(), 
        OrderId = order.Id, 
        To = customer.Email,
        Subject = $"Order #{order.Id} confirmation",
        Body = $"Your order #{order.Id} is confirmed. Total: {order.TotalAmount:F2}.",
        Attempts = attempts
    };
}
