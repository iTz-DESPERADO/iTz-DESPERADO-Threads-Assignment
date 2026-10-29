using System.Diagnostics;
using OrderProcessing.Shared.Logging;

namespace OrderProcessing.Phase2;

public static class BlockingDemo
{
    public static void Run(CancellationToken ct)
    {
        Task delay = Task.Delay(200, ct);

        Stopwatch timer = Stopwatch.StartNew();

        Log.Info("Blocking demo: calling Wait on an unfinished task.");

        delay.Wait(ct);
        
        Log.Info($"Blocking demo: caller blocked for {timer.ElapsedMilliseconds} ms; status = {delay.Status}.");
    }
}
