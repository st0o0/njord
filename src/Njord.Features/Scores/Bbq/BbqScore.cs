using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Shared;
using Njord.Messages.Pipeline;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Scores.Bbq;

public sealed class BbqScore(IOptions<BbqConfig> config) : FeatureRule<ForecastInput>
{
    public const double GoodHourScore = 70.0;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Score,
        FeatureCategory.Lifestyle,
        Name: "Bbq",
        DefaultEnabled: false,
        new DomainRequirements(Forecast: ForecastParams.Precipitation | ForecastParams.Temperature | ForecastParams.WindSpeed
            | ForecastParams.PrecipitationProbability | ForecastParams.CloudCover,
            OptionalForecast: ForecastParams.IsDay));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx)
    {
        var hours = ctx.Current.Forecast.NextHours(ctx.Now);
        if (hours.Count == 0)
        {
            return null;
        }

        var (score, start, length) = Scoring.BestWindow([.. hours.Select(ScoreHour)], GoodHourScore);
        return new BbqScoreResult(ctx.Location, score, hours[start].ValidFrom, length);
    }

    private static double ScoreHour(ForecastPoint point)
    {
        if (point.IsDay is not > 0 || point.Precipitation > 0.1)
        {
            return 0.0;
        }

        if (point is not { Temperature: { } air, WindSpeed: { } windSpeed, PrecipitationProbability: { } probability, CloudCover: { } cloudCover })
        {
            return 0.0;
        }

        var temperature = Scoring.Band(air, 10.0, 18.0, 28.0, 36.0);
        var wind = 1.0 - Scoring.Ramp(windSpeed, 5.0, 10.0);
        var rain = 1.0 - Scoring.Ramp(probability, 0.0, 60.0);
        var cloud = 1.0 - Scoring.Ramp(cloudCover, 40.0, 100.0);
        return 100.0 * (0.35 * temperature + 0.2 * wind + 0.3 * rain + 0.15 * cloud);
    }
}
