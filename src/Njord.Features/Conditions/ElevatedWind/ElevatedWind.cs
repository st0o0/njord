using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Results;
using Njord.Messages.Results.Conditions;
using Njord.Messages.Stream;

namespace Njord.Features.Conditions.ElevatedWind;

public sealed class ElevatedWind(IOptions<ElevatedWindConfig> config) : FeatureRule<ForecastInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Energy,
        Name: "ElevatedWind",
        new DomainRequirements(Forecast: ForecastParams.WindSpeed80M | ForecastParams.WindDirection80M,
            OptionalForecast: ForecastParams.WindSpeed120M | ForecastParams.WindDirection120M),
        EntityMetadata: new EntityMetadata(EntityPlatform.BinarySensor));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx)
    {
        var window = ctx.Current.Forecast.NextHoursInclusive(ctx.Now);
        var current = window.FirstOrDefault();
        if (current is not { WindSpeed80M: { } speed80, WindDirection80M: { } dir80 })
        {
            return null;
        }

        return new ElevatedWindResult(
            ctx.Location,
            Speed80M: Math.Round(speed80, 1),
            Direction80M: Math.Round(dir80, 0),
            Speed120M: current.WindSpeed120M is { } s120 ? Math.Round(s120, 1) : null,
            Direction120M: current.WindDirection120M is { } d120 ? Math.Round(d120, 0) : null);
    }
}
