using OrderProcessing.Shared.Cancellation;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Storage;

using RunCancellation cancellation = new();
JsonFileStore store = new();
DataSeeder seeder = new(store);

try
{
    await seeder.ResetAsync(cancellation.Token);

    Log.Success("Initial data is ready.");
}
catch (OperationCanceledException) when (cancellation.Token.IsCancellationRequested)
{
    Log.Info("Data initialization cancelled.");
}
