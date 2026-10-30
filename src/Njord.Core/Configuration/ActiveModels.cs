namespace Njord.Core.Configuration;

public sealed record ActiveModels
{
    public const string BestMatch = "best_match";

    public string PrimaryForecastModel { get; init; } = BestMatch;

    public IReadOnlyList<string> ForecastModels { get; init; } = [];

    public IReadOnlyList<string> EnsembleModels { get; init; } = [];

    public IReadOnlyList<string> AllModels =>
        ForecastModels.Count > 1
            ? [PrimaryForecastModel, .. ForecastModels.Skip(1), .. EnsembleModels]
            : [PrimaryForecastModel, .. EnsembleModels];
}
