using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Observations.SnowDepth;

public sealed class SnowDepthObservation(IOptions<SnowDepthObservationConfig> config) : FeatureRule<ForecastInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Observation,
        FeatureCategory.Forecast,
        Name: "SnowDepthObservation",
        new DomainRequirements(Forecast: ForecastParams.SnowDepth),
        EntityMetadata: new EntityMetadata(EntityPlatform.Sensor, DeviceClass: "distance", Unit: "m"));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } d ? new ForecastInput(d) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx) =>
        ForecastQueries.ValueAt(ctx.Current.Forecast, ctx.Now) is { SnowDepth: { } value }
            ? new ObservationResult(ctx.Location, Descriptor.Name, value)
            : null;
}
