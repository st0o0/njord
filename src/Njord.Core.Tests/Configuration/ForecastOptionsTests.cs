using Njord.Core.Configuration;

namespace Njord.Core.Tests.Configuration;

public sealed class ForecastOptionsTests
{
    [Fact]
    public void Apply_defaults_keeps_models_empty_meaning_best_match()
    {
        var options = new ForecastOptions();

        options.ApplyDefaults();

        Assert.Empty(options.Models);
    }

    [Fact]
    public void Apply_defaults_keeps_configured_models()
    {
        var options = new ForecastOptions { Models = ["icon_seamless"] };

        options.ApplyDefaults();

        Assert.Equal(["icon_seamless"], options.Models);
    }
}
