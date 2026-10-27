using OrderProcessing.Phase1;
using OrderProcessing.Shared.Cancellation;
using OrderProcessing.Shared.Storage;

int? cancelAfter = null;
int index = args.Length > 0 && args[0].Equals("phase1", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
if (index < args.Length)
{
    if (args.Length - index != 2 || args[index] != "--cancel-after"
        || !int.TryParse(args[index + 1], out int milliseconds) || milliseconds < 0)
    {
        Console.Error.WriteLine("Usage: dotnet run --project OrderProcessing -- [phase1] [--cancel-after <ms>]");
        return;
    }
    cancelAfter = milliseconds;
}

using RunCancellation cancellation = new(cancelAfter);
await new Phase1Runner(new JsonFileStore()).RunAsync(cancellation.Token);
