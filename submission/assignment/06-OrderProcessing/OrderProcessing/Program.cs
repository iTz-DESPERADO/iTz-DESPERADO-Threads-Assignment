using OrderProcessing.Phase1;
using OrderProcessing.Phase2;
using OrderProcessing.Phase3;
using OrderProcessing.Shared.Cancellation;
using OrderProcessing.Shared.Storage;

int? cancelAfter = null;

string phase = "phase1";

int index = 0;

if (args.Length > 0 && (args[0].Equals("phase1", StringComparison.OrdinalIgnoreCase)
    || args[0].Equals("phase2", StringComparison.OrdinalIgnoreCase)
    || args[0].Equals("phase3", StringComparison.OrdinalIgnoreCase)
    || args[0].Equals("phase3-compare", StringComparison.OrdinalIgnoreCase)
    || args[0].Equals("phase3-fire-and-forget", StringComparison.OrdinalIgnoreCase)))
{
    phase = args[0].ToLowerInvariant();

    index = 1;
}
if (index < args.Length)
{
    if (args.Length - index != 2 || args[index] != "--cancel-after"
        || !int.TryParse(args[index + 1], out int milliseconds) || milliseconds < 0)
    {
        Console.Error.WriteLine("Usage: dotnet run --project OrderProcessing -- [phase1|phase2|phase3|phase3-compare|phase3-fire-and-forget] [--cancel-after <ms>]");
        return;
    }
    cancelAfter = milliseconds;
}

using RunCancellation cancellation = new(cancelAfter);

if (phase == "phase3")
    await new Phase3Runner(new JsonFileStore()).RunAsync(cancellation.Token);

else if (phase == "phase3-compare")
    await new Phase3ComparisonRunner(new JsonFileStore()).RunAsync(cancellation.Token);

else if (phase == "phase3-fire-and-forget")
    await new FireAndForgetRunner(new JsonFileStore()).RunAsync(cancellation.Token);

else if (phase == "phase2")
    await new Phase2Runner(new JsonFileStore()).RunAsync(cancellation.Token);

else
    await new Phase1Runner(new JsonFileStore()).RunAsync(cancellation.Token);
