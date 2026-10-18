namespace Njord.Weather;

public sealed record ModelForecasts
{
    public static readonly ModelForecasts Empty = new(
        new Dictionary<(string Location, WeatherModel Model), Forecast>(), false);

    public IReadOnlyDictionary<(string Location, WeatherModel Model), Forecast> Entries { get; }
    public bool HasChanged { get; }

    private readonly Dictionary<(string Location, WeatherModel Model), Forecast> _entries;

    private ModelForecasts(
        Dictionary<(string Location, WeatherModel Model), Forecast> entries,
        bool hasChanged)
    {
        _entries = entries;
        Entries = entries;
        HasChanged = hasChanged;
    }

    public ModelForecasts Update(Forecast forecast)
    {
        var key = (forecast.Location, forecast.Model);

        if (_entries.TryGetValue(key, out var existing) && existing.Cycle == forecast.Cycle)
        {
            return new ModelForecasts(_entries, false);
        }

        var copy = new Dictionary<(string Location, WeatherModel Model), Forecast>(_entries)
        {
            [key] = forecast,
        };
        return new ModelForecasts(copy, true);
    }

    public IReadOnlyList<WeatherModel> ModelsFor(string location)
    {
        var models = new List<WeatherModel>();
        foreach (var (key, _) in _entries)
        {
            if (key.Location == location)
            {
                models.Add(key.Model);
            }
        }
        return models;
    }
}
