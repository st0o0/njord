namespace Njord.Core.Configuration;

public sealed class NjordOptions
{
    public const string SectionName = "Njord";

    public IList<LocationOptions> Locations { get; set; } = [];

    public IList<string> Models { get; set; } = [];

    public IList<int> Horizons { get; set; } = [3, 6, 12, 24, 48, 72];

    public int ForecastDays { get; set; } = 4;

    public ParameterOptions Parameters { get; set; } = new();
}
