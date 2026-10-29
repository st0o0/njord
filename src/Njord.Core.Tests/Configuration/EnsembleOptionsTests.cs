using Njord.Core.Configuration;

namespace Njord.Core.Tests.Configuration;

public sealed class EnsembleOptionsTests
{
    [Fact]
    public void Apply_defaults_fills_the_default_model_when_empty()
    {
        var options = new EnsembleOptions();

        options.ApplyDefaults();

        Assert.Equal([EnsembleOptions.DefaultModel], options.Models);
    }

    [Fact]
    public void Apply_defaults_keeps_configured_models()
    {
        var options = new EnsembleOptions { Models = ["icon_seamless_eps"] };

        options.ApplyDefaults();

        Assert.Equal(["icon_seamless_eps"], options.Models);
    }
}
