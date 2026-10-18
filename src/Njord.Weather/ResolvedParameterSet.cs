namespace Njord.Weather;

public sealed class ResolvedParameterSet
{
    private readonly HashSet<ParameterDef> _hourlySet;
    private readonly HashSet<ParameterDef> _dailySet;

    public ResolvedParameterSet(IReadOnlyList<ParameterDef> hourly, IReadOnlyList<ParameterDef> daily)
    {
        Hourly = hourly;
        Daily = daily;
        _hourlySet = [.. hourly];
        _dailySet = [.. daily];
    }

    public IReadOnlyList<ParameterDef> Hourly { get; }
    public IReadOnlyList<ParameterDef> Daily { get; }

    public int HourlyCount => Hourly.Count;
    public int ApiCallWeight => (int)Math.Ceiling(HourlyCount / 10.0);

    public ParameterDef? Get(ParameterDef param)
        => _hourlySet.TryGetValue(param, out var found) ? found
         : _dailySet.TryGetValue(param, out found) ? found
         : null;

    public bool Contains(ParameterDef param)
        => _hourlySet.Contains(param) || _dailySet.Contains(param);
}
