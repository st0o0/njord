using Njord.Tests.Shared;

namespace Njord.IntegrationTests;

// Bounded retry for state that becomes true asynchronously in a real host (no TestKit here). The last assertion
// failure is rethrown when the bound is reached, so the test names what it was waiting for.
internal static class Eventually
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(100);

    public static async Task AssertAsync(Func<Task> assertion, CancellationToken cancellationToken, TimeSpan? within = null)
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bound.CancelAfter(within ?? TestTimeouts.RealTimeWaitMax);
        using var timer = new PeriodicTimer(Interval);
        Exception? last = null;
        try
        {
            do
            {
                try
                {
                    await assertion();
                    return;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    last = ex;
                }
            }
            while (await timer.WaitForNextTickAsync(bound.Token));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }

        throw new TimeoutException($"Condition not met within {within ?? TestTimeouts.RealTimeWaitMax}.", last);
    }

    public static async Task<T> ValueAsync<T>(Func<Task<T?>> probe, CancellationToken cancellationToken, TimeSpan? within = null)
        where T : class
    {
        T? found = null;
        await AssertAsync(async () =>
        {
            found = await probe();
            Assert.NotNull(found);
        }, cancellationToken, within);
        return found!;
    }
}
