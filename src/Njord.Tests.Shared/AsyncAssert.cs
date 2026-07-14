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
}
