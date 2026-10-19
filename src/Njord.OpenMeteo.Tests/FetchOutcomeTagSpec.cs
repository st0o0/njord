using Njord.Forecast;
using Njord.Model;

namespace Njord.OpenMeteo.Tests;

public sealed class FetchOutcomeTagSpec
{
    [Theory]
    [InlineData(PollFailureReason.RateLimited, "ratelimited")]
    [InlineData(PollFailureReason.ModelUnavailable, "modelunavailable")]
    [InlineData(PollFailureReason.MalformedPayload, "malformedpayload")]
    [InlineData(PollFailureReason.Transport, "transport")]
    public void Failure_reason_has_the_established_label(PollFailureReason reason, string expected) =>
        Assert.Equal(expected, FetchOutcomeTag.For(reason));

    [Fact]
    public void Every_failure_reason_has_a_label()
    {
        foreach (var reason in Enum.GetValues<PollFailureReason>())
        {
            Assert.False(string.IsNullOrEmpty(FetchOutcomeTag.For(reason)));
        }
    }

    [Fact]
    public void Undefined_failure_reason_throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => FetchOutcomeTag.For((PollFailureReason)99));

        Assert.Equal("reason", ex.ParamName);
    }

    [Fact]
    public void Failure_outcome_is_labelled_by_its_reason()
    {
        var outcome = new PollOutcome.Failure("home", new WeatherModel("icon_eu"), PollFailureReason.RateLimited, "429");

        Assert.Equal("ratelimited", FetchOutcomeTag.For(outcome));
    }
}
