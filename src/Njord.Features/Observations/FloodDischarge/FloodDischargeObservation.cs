using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Flood;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Observations.FloodDischarge;

public sealed class FloodDischargeObservation(IOptions<FloodDischargeObservationConfig> config) : FeatureRule<FloodInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Observation,
        FeatureCategory.Flood,
        Name: "FloodDischargeObservation",
        new DomainRequirements(Flood: FloodParams.Discharge),
        EntityMetadata: new EntityMetadata(EntityPlatform.Sensor, Unit: "m³/s"));

    public override bool Enabled => config.Value.Enabled;

    protected override FloodInput? Project(LocationForecast forecast) =>
        forecast.Flood is { } d ? new(d) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<FloodInput> ctx) =>
        ForecastQueries.ValueAt(ctx.Current.Flood, ctx.Now) is { Discharge: { } value }
            ? new ObservationResult(ctx.Location, value)
            : null;
}
