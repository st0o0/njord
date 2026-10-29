using Njord.Core.Configuration;
using Njord.OpenMeteo.Configuration;
using Njord.Tests.Shared;

namespace Njord.OpenMeteo.Tests.Configuration;

public sealed class EnsembleOptionsValidatorTests
{
    private readonly EnsembleOptionsValidator _validator = new();

    [Fact]
    public void Should_accept_the_default_model()
    {
        var result = _validator.Validate(null, new EnsembleOptions { Models = [EnsembleOptions.DefaultModel] });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Should_accept_models_unknown_to_the_catalog()
    {
        var result = _validator.Validate(null, new EnsembleOptions { Models = ["some_future_model"] });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(36)]
    public void Should_reject_forecast_days_outside_one_to_thirty_five(int days)
    {
        var result = _validator.Validate(null, new EnsembleOptions { ForecastDays = days, Models = [EnsembleOptions.DefaultModel] });

        Assert.Contains("Ensemble.ForecastDays must be between 1 and 35", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_an_empty_model_list()
    {
        var result = _validator.Validate(null, new EnsembleOptions { Models = [] });

        Assert.Equal("Ensemble.Models must contain at least one model.", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_a_duplicate_model_naming_it()
    {
        var result = _validator.Validate(null, new EnsembleOptions { Models = ["a", "a"] });

        Assert.Equal("Ensemble.Models contains 'a' more than once.", ValidationAssert.SingleFailure(result));
    }
}
