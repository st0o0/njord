using Njord.Core.Configuration.Polling;

namespace Njord.Core.Tests.Configuration;

public sealed class FetchTimeoutsSpec
{
    [Fact]
    public void The_default_reply_timeout_outlasts_admission_and_the_http_timeout()
    {
        var timeouts = FetchTimeouts.Default;

        Assert.Multiple(
            () => Assert.Equal(TimeSpan.FromSeconds(20), timeouts.Admission),
            () => Assert.Equal(TimeSpan.FromSeconds(30), timeouts.Http),
            () => Assert.Equal(TimeSpan.FromSeconds(60), timeouts.Reply),
            () => Assert.True(timeouts.Reply > timeouts.Admission + timeouts.Http,
                $"Reply {timeouts.Reply} must exceed admission {timeouts.Admission} + HTTP {timeouts.Http}"));
    }
}
