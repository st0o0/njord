using Akka.Actor;
using Akka.Configuration;
using Njord.Actors;

namespace Njord.Core.Tests.Actors;

public sealed class RetryBackoffSpec
{
    [Fact]
    public void Default_delay_doubles_per_attempt_and_caps_at_30_seconds()
    {
        var expected = new[] { 1, 2, 4, 8, 16, 30, 30, 30 };

        var actual = Enumerable.Range(0, expected.Length)
            .Select(attempt => (int)RetryBackoff.Default(attempt).TotalSeconds)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact(Timeout = 30000)]
    public async Task For_uses_the_default_sequence_when_no_override_is_configured()
    {
        var system = ActorSystem.Create("retry-backoff-default");
        try
        {
            Assert.Equal(TimeSpan.FromSeconds(1), RetryBackoff.For(system, 0));
            Assert.Equal(TimeSpan.FromSeconds(8), RetryBackoff.For(system, 3));
            Assert.Equal(TimeSpan.FromSeconds(30), RetryBackoff.For(system, 9));
        }
        finally
        {
            await system.Terminate().WaitAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact(Timeout = 30000)]
    public async Task For_uses_the_configured_override_for_every_attempt()
    {
        var config = ConfigurationFactory.ParseString($"{RetryBackoff.OverridePath} = 10ms");
        var system = ActorSystem.Create("retry-backoff-override", config);
        try
        {
            Assert.Equal(TimeSpan.FromMilliseconds(10), RetryBackoff.For(system, 0));
            Assert.Equal(TimeSpan.FromMilliseconds(10), RetryBackoff.For(system, 9));
        }
        finally
        {
            await system.Terminate().WaitAsync(TestContext.Current.CancellationToken);
        }
    }
}
