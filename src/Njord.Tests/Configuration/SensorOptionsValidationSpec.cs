using Njord.Configuration;

namespace Njord.Tests.Configuration;

public sealed class SensorOptionsValidationSpec
{
    [Fact(Timeout = 5000)]
    public void default_options_accepted()
    {
        var result = new SensorOptionsValidator().Validate(null, new SensorOptions());
        Assert.True(result.Succeeded);
    }

    [Fact(Timeout = 5000)]
    public void positive_staleness_accepted()
    {
        var result = new SensorOptionsValidator().Validate(null, new SensorOptions { StalenessSeconds = 3600 });
        Assert.True(result.Succeeded);
    }

    [Theory(Timeout = 5000)]
    [InlineData(0)]
    [InlineData(-1)]
    public void non_positive_staleness_rejected(int staleness)
    {
        var result = new SensorOptionsValidator().Validate(null, new SensorOptions { StalenessSeconds = staleness });
        Assert.True(result.Failed);
        Assert.Contains("StalenessSeconds", result.FailureMessage);
    }
}
