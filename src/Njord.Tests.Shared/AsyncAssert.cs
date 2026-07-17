namespace Njord.Tests.Shared;

public static class AsyncAssert
{
    public static async Task WaitUntil(
        Func<bool> condition,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(3);
        var interval = pollInterval ?? TimeSpan.FromMilliseconds(25);
        using var cts = new CancellationTokenSource(effectiveTimeout);

        while (!condition())
        {
            try
            {
                await Task.Delay(interval, cts.Token);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException(
                    $"Condition was not met within {effectiveTimeout.TotalMilliseconds} ms.");
            }
        }
    }

    public static async Task WaitUntil(
        Func<Task<bool>> condition,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(3);
        var interval = pollInterval ?? TimeSpan.FromMilliseconds(25);
        using var cts = new CancellationTokenSource(effectiveTimeout);

        while (!await condition())
        {
            try
            {
                await Task.Delay(interval, cts.Token);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException(
                    $"Condition was not met within {effectiveTimeout.TotalMilliseconds} ms.");
            }
        }
    }

    public static async Task StaysTrue(
        Func<bool> condition,
        TimeSpan? duration = null,
        TimeSpan? pollInterval = null)
    {
        var effectiveDuration = duration ?? TimeSpan.FromMilliseconds(300);
        var interval = pollInterval ?? TimeSpan.FromMilliseconds(25);
        using var cts = new CancellationTokenSource(effectiveDuration);

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                if (!condition())
                    throw new Exception("Condition became false during observation period.");
                await Task.Delay(interval, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
        }

        if (!condition())
            throw new Exception("Condition was false at end of observation period.");
    }

    public static async Task StaysTrue(
        Func<Task<bool>> condition,
        TimeSpan? duration = null,
        TimeSpan? pollInterval = null)
    {
        var effectiveDuration = duration ?? TimeSpan.FromMilliseconds(300);
        var interval = pollInterval ?? TimeSpan.FromMilliseconds(25);
        using var cts = new CancellationTokenSource(effectiveDuration);

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                if (!await condition())
                    throw new Exception("Condition became false during observation period.");
                await Task.Delay(interval, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
        }

        if (!await condition())
            throw new Exception("Condition was false at end of observation period.");
    }
}
