using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Marine;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Observations.WavePeriod;

public sealed class WavePeriodObservation(IOptions<WavePeriodObservationConfig> config) : FeatureRule<MarineInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Observation,
        FeatureCategory.Marine,
        Name: "WavePeriodObservation",
        new DomainRequirements(Marine: MarineParams.WavePeriod),
        EntityMetadata: new EntityMetadata(EntityPlatform.Sensor, DeviceClass: "duration", Unit: "s"));

    public override bool Enabled => config.Value.Enabled;

    protected override MarineInput? Project(LocationForecast forecast) =>
        forecast.Marine is { } d ? new(d) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<MarineInput> ctx) =>
        ForecastQueries.ValueAt(ctx.Current.Marine, ctx.Now) is { WavePeriod: { } value }
            ? new ObservationResult(ctx.Location, Descriptor.Name, value)
            : null;
}
