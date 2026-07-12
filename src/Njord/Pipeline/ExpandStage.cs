using Microsoft.Extensions.Logging;
using Njord.Configuration;
using Njord.Domain;

namespace Njord.Pipeline;

public static class ExpandStage
{
    public static IEnumerable<WeightedTarget> Expand(
        PipelineCommand command,
        NjordOptions options,
        ResolvedParameterSet parameters,
        ILogger logger)
    {
        var weight = WeightedTarget.ComputeWeight(parameters.HourlyCount, options.ForecastDays);
        var locationsByName = options.Locations.ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);

        switch (command)
        {
            case PipelineCommand.RefreshLocation refresh:
                if (!locationsByName.TryGetValue(refresh.Location, out var loc))
                {
                    logger.LogWarning("Ignoring RefreshLocation for unknown location {Location}", refresh.Location);
                    yield break;
                }
                foreach (var modelId in options.Models)
                    yield return new WeightedTarget(loc, new WeatherModel(modelId), weight);
                break;

            case PipelineCommand.RefreshModel refreshModel:
                if (!locationsByName.TryGetValue(refreshModel.Location, out var modelLoc))
                {
                    logger.LogWarning("Ignoring RefreshModel for unknown location {Location}", refreshModel.Location);
                    yield break;
                }
                if (!options.Models.Contains(refreshModel.Model.Id, StringComparer.OrdinalIgnoreCase))
                {
                    logger.LogWarning("Ignoring RefreshModel for unknown model {Model}", refreshModel.Model.Id);
                    yield break;
                }
                yield return new WeightedTarget(modelLoc, refreshModel.Model, weight);
                break;
        }
    }
}
