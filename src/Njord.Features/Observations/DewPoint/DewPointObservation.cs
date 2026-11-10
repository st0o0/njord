using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Observations.DewPoint;

public sealed class DewPointObservation(IOptions<DewPointObservationConfig> config) : FeatureRule<ForecastInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Observation,
        FeatureCategory.Forecast,
        Name: "DewPointObservation",
        new DomainRequirements(Forecast: ForecastParams.DewPoint),
        EntityMetadata: new EntityMetadata(EntityPlatform.Sensor, DeviceClass: "temperature", Unit: "°C"));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } d ? new ForecastInput(d) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx) =>
        ForecastQueries.ValueAt(ctx.Current.Forecast, ctx.Now) is { DewPoint: { } value }
            ? new ObservationResult(ctx.Location, Descriptor.Name, value)
            : null;
}
