
using OrderProcessing.Shared.Logging;

namespace OrderProcessing.Shared.Simulation;

public static class SimulatedLatency
{
    public static async Task StepAsync(string actor, string step, int milliseconds, CancellationToken ct)
    {
        Log.Info($"{actor} -> {step}...");

        await Task.Delay(milliseconds, ct);
    }

    public static void Step(string actor, string step, int milliseconds, CancellationToken ct = default)
    {
        Log.Info($"{actor} -> {step}...");

        ct.ThrowIfCancellationRequested();

        Thread.Sleep(milliseconds);

        ct.ThrowIfCancellationRequested();
    }

    public static async Task<T> DatabaseQueryAsync<T>(
        string actor, Func<CancellationToken, Task<T>> read, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(read);

        await StepAsync(actor, "Opening connection", 200, ct);

        await StepAsync(actor, "Executing query", Random.Shared.Next(300, 601), ct);

        ct.ThrowIfCancellationRequested();

        return await read(ct);
    }

    public static T DatabaseQuery<T>(string actor, Func<T> read, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(read);

        ct.ThrowIfCancellationRequested();

        Step(actor, "Opening connection", 200, ct);

        ct.ThrowIfCancellationRequested();

        Step(actor, "Executing query", Random.Shared.Next(300, 601), ct);

        ct.ThrowIfCancellationRequested();

        return read();
    }

    public static async Task DatabaseWriteAsync(
        string actor, Func<CancellationToken, Task> write, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(write);

        await StepAsync(actor, "Opening connection", 200, ct);

        await StepAsync(actor, "Beginning transaction", 100, ct);

        await StepAsync(actor, "Inserting/updating", 400, ct);

        ct.ThrowIfCancellationRequested();

        await write(ct); 

        await StepAsync(actor, "Committing", 200, ct);

    }

    public static void DatabaseWrite(string actor, Action write, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(write);

        Step(actor, "Opening connection", 200, ct);

        Step(actor, "Beginning transaction", 100, ct);

        Step(actor, "Inserting/updating", 400, ct);

        write();

        Step(actor, "Committing", 200, ct);
    }

    public static async Task ExternalApiAsync(string actor, CancellationToken ct)
    {
        await StepAsync(actor, "DNS/handshake", 150, ct);

        await StepAsync(actor, "Sending request", 300, ct);

        await StepAsync(actor, "Waiting for response", Random.Shared.Next(500, 1501), ct);
    }

    public static void ExternalApi(string actor, CancellationToken ct = default)
    {
        Step(actor, "DNS/handshake", 150, ct);

        Step(actor, "Sending request", 300, ct);

        Step(actor, "Waiting for response", Random.Shared.Next(500, 1501), ct);
    }

    public static async Task PaymentGatewayAsync(string actor, CancellationToken ct)
    {
        await StepAsync(actor, "Authorizing", 800, ct);

        await StepAsync(actor, "Fraud check", 500, ct);

        await StepAsync(actor, "Capturing", 700, ct);
    }

    public static void PaymentGateway(string actor, CancellationToken ct = default)
    {
        Step(actor, "Authorizing", 800, ct);

        Step(actor, "Fraud check", 500, ct);

        Step(actor, "Capturing", 700, ct);
    }

    public static async Task EmailAsync(string actor, CancellationToken ct)
    {
        await StepAsync(actor, "Rendering template", 200, ct);

        await StepAsync(actor, "Connecting to SMTP", 300, ct);

        await StepAsync(actor, "Sending", 500, ct);
    }

    public static void Email(string actor, CancellationToken ct = default)
    {
        Step(actor, "Rendering template", 200, ct);

        Step(actor, "Connecting to SMTP", 300, ct);

        Step(actor, "Sending", 500, ct);
    }
}
