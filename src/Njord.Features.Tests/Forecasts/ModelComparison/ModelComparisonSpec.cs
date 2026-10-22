using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Forecasts.ModelComparison;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Forecasts;

public sealed class ModelComparisonSpec
{
    private static ModelComparison Feature() => new(Options.Create(new ModelComparisonConfig { Enabled = true }));

    private static LocationForecast With(params (string Id, double Temp, double Rain)[] models)
    {
        var forecast = WeatherOnly(Uniform(p => p));
        foreach (var (id, temp, rain) in models)
        {
            forecast = forecast.WithModel(id, Uniform(p => p with { Temperature = temp, Precipitation = rain }));
        }

        return forecast;
    }

    [Fact]
    public void Every_model_reports_its_daily_extremes_and_precipitation_total()
    {
        var result = Assert.IsType<ModelComparisonResult>(Run(Feature(), Now, With(("b", 12.04, 0.5), ("a", 8.0, 0.0))));

        var day = result.Days[0];
        Assert.Equal(
            [
                new ModelDayValues("a", 8.0, 8.0, 0.0),
                new ModelDayValues("b", 12.0, 12.0, 6.0),
            ],
            day.Models);
    }

    [Fact]
    public void Nothing_is_reported_with_a_single_model()
    {
        Assert.Null(Run(Feature(), Now, With(("a", 8.0, 0.0))));
    }

    [Fact]
    public void Nothing_is_reported_without_model_data()
    {
        Assert.Null(Run(Feature(), Now, WeatherOnly(Day())));
    }
}
