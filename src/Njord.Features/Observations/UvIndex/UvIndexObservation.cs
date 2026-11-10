using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Observations.UvIndex;

public sealed class UvIndexObservation(IOptions<UvIndexObservationConfig> config) : FeatureRule<ForecastInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Observation,
        FeatureCategory.Forecast,
        Name: "UvIndexObservation",
        new DomainRequirements(Forecast: ForecastParams.UvIndex),
        EntityMetadata: new EntityMetadata(EntityPlatform.Sensor, Unit: "index"));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } d ? new(d) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx) =>
        ForecastQueries.ValueAt(ctx.Current.Forecast, ctx.Now) is { UvIndex: { } value }
            ? new ObservationResult(ctx.Location, Descriptor.Name, value)
            : null;
}
