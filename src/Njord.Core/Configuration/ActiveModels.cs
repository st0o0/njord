namespace Njord.Core.Configuration;

public sealed record ActiveModels
{
    public const string BestMatch = "best_match";

    public string PrimaryForecastModel { get; set; } = BestMatch;

    public IReadOnlyList<string> ForecastModels { get; set; } = [];

    public IReadOnlyList<string> EnsembleModels { get; set; } = [];

    public IReadOnlyList<string> AllModels =>
        ForecastModels.Count > 1
            ? [PrimaryForecastModel, .. ForecastModels.Skip(1), .. EnsembleModels]
            : [PrimaryForecastModel, .. EnsembleModels];
}
