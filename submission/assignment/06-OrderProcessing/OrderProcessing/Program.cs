using OrderProcessing.Phase1;
using OrderProcessing.Phase2;
using OrderProcessing.Phase3;
using OrderProcessing.Phase4;
using OrderProcessing.Shared.Cancellation;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        string[] modes = ["phase1", "phase2", "phase3", "phase4", "all", "phase3-compare", "phase3-fire-and-forget"];

        string mode;

        int? cancelAfter = null;
        
        if (args.Length == 0)
        {
            Console.WriteLine("1. Phase 1\n
            2. Phase 2\n
            3. Phase 3\n
            4. Phase 4\n
            5. All phases\n
            6. Phase 3 sync/async comparison\n
            7. Phase 3 fire-and-forget investigation\n
            0. Exit");

            Console.Write("Select: ");

            if (!int.TryParse(Console.ReadLine(), out int selection) || selection == 0)
             return;

            if (selection < 1 || selection > modes.Length) 
            { 
                Console.Error.WriteLine("Invalid selection.");
             return; 
             }

            mode = modes[selection - 1];
        }
        else
        {
            mode = args[0].ToLowerInvariant();

            if (!modes.Contains(mode) || (args.Length != 1 && args.Length != 3)) 
            { 
                PrintUsage(); 

                return;
            }

            if (args.Length == 3)
            {
                if (args[1] != "--cancel-after" || !int.TryParse(args[2], out int milliseconds) || milliseconds < 0)
                { 
                    PrintUsage(); 
                    return;
                }

                cancelAfter = milliseconds;
            }
        }

        string[] selected = mode == "all" ? ["phase1", "phase2", "phase3", "phase4"] : [mode];

        foreach (string phase in selected)
        {
            using RunCancellation cancellation = new(cancelAfter);

            JsonFileStore store = new();

            switch (phase)
            {
                case "phase1": await new Phase1Runner(store).RunAsync(cancellation.Token); break;

                case "phase2": await new Phase2Runner(store).RunAsync(cancellation.Token); break;

                case "phase3": await new Phase3Runner(store).RunAsync(cancellation.Token); break;

                case "phase4": await new Phase4Runner(store).RunAsync(cancellation.Token); break;

                case "phase3-compare": await new Phase3ComparisonRunner(store).RunAsync(cancellation.Token); break;
                
                case "phase3-fire-and-forget": await new FireAndForgetRunner(store).RunAsync(cancellation.Token); break;
            }

            if (cancellation.Token.IsCancellationRequested) 
                 break;
        }
    }

    private static void PrintUsage() => Console.Error.WriteLine(
        "Usage: dotnet run --project OrderProcessing -- phase1|phase2|phase3|phase4|all|phase3-compare|phase3-fire-and-forget [--cancel-after <ms>]");
}
