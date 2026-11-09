using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Observations.WindGusts;

public sealed class WindGustsObservation(IOptions<WindGustsObservationConfig> config) : FeatureRule<ForecastInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Observation,
        FeatureCategory.Forecast,
        Name: "WindGustsObservation",
        new DomainRequirements(Forecast: ForecastParams.WindGusts),
        EntityMetadata: new EntityMetadata(EntityPlatform.Sensor, DeviceClass: "wind_speed", Unit: "m/s"));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } d ? new(d) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx) =>
        ForecastQueries.ValueAt(ctx.Current.Forecast, ctx.Now) is { WindGusts: { } value }
            ? new ObservationResult(ctx.Location, Descriptor.Name, value)
            : null;
}
