using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Results;
using Njord.Messages.Results.Conditions;
using Njord.Messages.Stream;

namespace Njord.Features.Conditions.CommuteSafety;

public sealed class CommuteSafety(IOptions<CommuteSafetyConfig> config) : FeatureRule<ForecastInput>
{
    public const double IceTemperature = 1.0;
    public const double DewPointSpread = 2.0;
    public const double FogHumidity = 95.0;
    public const double HeavyRain = 5.0;
    public const double StrongWind = 60.0 / 3.6;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Lifestyle,
        Name: "Commute",
        DefaultEnabled: true,
        new DomainRequirements(Forecast: ForecastParams.Temperature | ForecastParams.DewPoint | ForecastParams.Humidity
            | ForecastParams.Precipitation | ForecastParams.WindSpeed));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx)
    {
        var hours = ctx.Current.Forecast.NextHours(ctx.Now, 2);
        if (hours.Count == 0)
        {
            return null;
        }

        var ice = hours.Any(p => p.Temperature < IceTemperature && p.Temperature - p.DewPoint < DewPointSpread);
        var fog = hours.Any(p => p.Humidity > FogHumidity && p.Temperature - p.DewPoint < DewPointSpread);
        var rain = hours.Any(p => p.Precipitation > HeavyRain);
        var wind = hours.Any(p => p.WindSpeed > StrongWind);

        var risks = new List<CommuteHazard>();
        if (ice)
        {
            risks.Add(CommuteHazard.Ice);
        }

        if (fog)
        {
            risks.Add(CommuteHazard.Fog);
        }

        if (rain)
        {
            risks.Add(CommuteHazard.HeavyRain);
        }

        if (wind)
        {
            risks.Add(CommuteHazard.StrongWind);
        }

        var level = risks.Count switch
        {
            0 => CommuteLevel.Safe,
            1 when !ice => CommuteLevel.Caution,
            _ => CommuteLevel.Dangerous,
        };
        return new CommuteSafetyResult(ctx.Location, level, [.. risks]);
    }
}
