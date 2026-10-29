using OrderProcessing.Shared.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace OrderProcessing.Shared.Cancellation;

public class RunCancellation : IDisposable
{
    private readonly CancellationTokenSource _source = new();
    public CancellationToken Token => _source.Token;

    public RunCancellation(int? cancelAfterMilliseconds = null)
    {
        if (cancelAfterMilliseconds is < 0)
            throw new ArgumentOutOfRangeException(nameof(cancelAfterMilliseconds));

        Console.CancelKeyPress += OnCancelKeyPress;

        if (cancelAfterMilliseconds == 0)
            _source.Cancel();

        else if (cancelAfterMilliseconds is { } timeout)
            _source.CancelAfter(timeout);
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;

        RequestCancellation();
    }

    public void RequestCancellation()
    {
        if (_source.IsCancellationRequested) 
            return;

        Log.Info("Cancellation requested.");

        _source.Cancel();
    }

    public void Dispose()
    {
        Console.CancelKeyPress -= OnCancelKeyPress;

        _source.Dispose();
    }
}
