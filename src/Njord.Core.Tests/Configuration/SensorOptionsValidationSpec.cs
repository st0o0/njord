using Njord.Core.Configuration;

namespace Njord.Core.Tests.Configuration;

public sealed class SensorOptionsValidationSpec
{
    [Fact]
    public void default_options_accepted()
    {
        var result = new SensorOptionsValidator().Validate(null, new SensorOptions());
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void positive_staleness_accepted()
    {
        var opts = new SensorOptions { StalenessSeconds = 3600 };
        var result = new SensorOptionsValidator().Validate(null, opts);
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void non_positive_staleness_rejected(int staleness)
    {
        var opts = new SensorOptions { StalenessSeconds = staleness };
        var result = new SensorOptionsValidator().Validate(null, opts);
        Assert.True(result.Failed);
        Assert.Contains("StalenessSeconds", result.FailureMessage);
    }
}
