using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Marine;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Observations.WaterTemperature;

public sealed class WaterTemperatureObservation(IOptions<WaterTemperatureObservationConfig> config) : FeatureRule<MarineInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Observation,
        FeatureCategory.Marine,
        Name: "WaterTemperatureObservation",
        new DomainRequirements(Marine: MarineParams.WaterTemperature),
        EntityMetadata: new EntityMetadata(EntityPlatform.Sensor, DeviceClass: "temperature", Unit: "°C"));

    public override bool Enabled => config.Value.Enabled;

    protected override MarineInput? Project(LocationForecast forecast) =>
        forecast.Marine is { } d ? new(d) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<MarineInput> ctx) =>
        ForecastQueries.ValueAt(ctx.Current.Marine, ctx.Now) is { WaterTemperature: { } value }
            ? new ObservationResult(ctx.Location, Descriptor.Name, value)
            : null;
}
