using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Marine;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Observations.WaveDirection;

public sealed class WaveDirectionObservation(IOptions<WaveDirectionObservationConfig> config) : FeatureRule<MarineInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Observation,
        FeatureCategory.Marine,
        Name: "WaveDirectionObservation",
        new DomainRequirements(Marine: MarineParams.WaveDirection),
        EntityMetadata: new EntityMetadata(EntityPlatform.Sensor, DeviceClass: "", Unit: "°"));

    public override bool Enabled => config.Value.Enabled;

    protected override MarineInput? Project(LocationForecast forecast) =>
        forecast.Marine is { } d ? new(d) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<MarineInput> ctx) =>
        ForecastQueries.ValueAt(ctx.Current.Marine, ctx.Now) is { WaveDirection: { } value }
            ? new ObservationResult(ctx.Location, Descriptor.Name, value)
            : null;
}
