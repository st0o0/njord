using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Forecasts.Shared;
using Njord.Messages.Results;
using Njord.Messages.Results.Forecasts;
using Njord.Messages.Stream;

namespace Njord.Features.Forecasts.ForecastModelComparison;

public sealed class ForecastModelComparison(
    IOptions<ForecastModelComparisonConfig> config,
    IOptions<ActiveModels> activeModels) : FeatureRule<ForecastModelsInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Forecast,
        FeatureCategory.Forecast,
        Name: "ForecastModelComparison",
        new DomainRequirements(Forecast: ForecastParams.Temperature),
        EntityMetadata: new EntityMetadata(EntityPlatform.Sensor));

    public override bool Enabled => config.Value.Enabled && activeModels.Value.ForecastModels.Count >= 2;

    protected override ForecastModelsInput? Project(LocationForecast forecast) =>
        forecast.ForecastModelMap is { Count: >= 2 } models ? new(models) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastModelsInput> ctx)
    {
        var days = ModelComparisonHelper.CompareDays(ctx.Current.Models, ctx.Now);
        return days.IsEmpty ? null : new ForecastModelComparisonResult(ctx.Location, days);
    }
}
