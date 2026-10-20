namespace Njord.Core.Domain;

public interface IClimateBaselineStore
{
    IReadOnlyDictionary<string, MonthlyBaseline[]> Baselines { get; }

    void Replace(IReadOnlyDictionary<string, MonthlyBaseline[]> baselines);
}

public sealed class ClimateBaselineStore : IClimateBaselineStore
{
    private volatile IReadOnlyDictionary<string, MonthlyBaseline[]> _baselines =
        new Dictionary<string, MonthlyBaseline[]>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, MonthlyBaseline[]> Baselines => _baselines;

    public void Replace(IReadOnlyDictionary<string, MonthlyBaseline[]> baselines) => _baselines = baselines;
}
