using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Marine;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Observations.WaveHeight;

public sealed class WaveHeightObservation(IOptions<WaveHeightObservationConfig> config) : FeatureRule<MarineInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Observation,
        FeatureCategory.Marine,
        Name: "WaveHeightObservation",
        new DomainRequirements(Marine: MarineParams.WaveHeight),
        EntityMetadata: new EntityMetadata(EntityPlatform.Sensor, Unit: "m"));

    public override bool Enabled => config.Value.Enabled;

    protected override MarineInput? Project(LocationForecast forecast) =>
        forecast.Marine is { } d ? new MarineInput(d) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<MarineInput> ctx) =>
        ForecastQueries.ValueAt(ctx.Current.Marine, ctx.Now) is { WaveHeight: { } value }
            ? new ObservationResult(ctx.Location, Descriptor.Name, value)
            : null;
}
