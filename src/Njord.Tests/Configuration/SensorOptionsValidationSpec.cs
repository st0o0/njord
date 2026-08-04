using Njord.Configuration;

namespace Njord.Tests.Configuration;

public sealed class SensorOptionsValidationSpec
{
    [Fact(Timeout = 5000)]
    public void default_options_accepted()
    {
        var result = new SensorOptionsValidator().Validate(null, new NjordOptions());
        Assert.True(result.Succeeded);
    }

    [Fact(Timeout = 5000)]
    public void positive_staleness_accepted()
    {
        var opts = new NjordOptions { Sensors = new SensorOptions { StalenessSeconds = 3600 } };
        var result = new SensorOptionsValidator().Validate(null, opts);
        Assert.True(result.Succeeded);
    }

    [Theory(Timeout = 5000)]
    [InlineData(0)]
    [InlineData(-1)]
    public void non_positive_staleness_rejected(int staleness)
    {
        var opts = new NjordOptions { Sensors = new SensorOptions { StalenessSeconds = staleness } };
        var result = new SensorOptionsValidator().Validate(null, opts);
        Assert.True(result.Failed);
        Assert.Contains("StalenessSeconds", result.FailureMessage);
    }
}
