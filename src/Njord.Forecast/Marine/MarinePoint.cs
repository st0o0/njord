namespace Njord.Forecast.Marine;

public sealed record MarinePoint(
    DateTimeOffset ValidFrom,
    double? WaveHeight = null,
    double? WavePeriod = null,
    double? WaveDirection = null,
    double? WaterTemperature = null) : IHasValidTime
{
    private static readonly MarineParams[] Flags =
        [.. Enum.GetValues<MarineParams>().Where(p => p != MarineParams.None)];

    public bool HasFields(MarineParams required) =>
        Flags.All(flag => !required.HasFlag(flag) || Has(flag));

    private bool Has(MarineParams param) => param switch
    {
        MarineParams.WaveHeight => WaveHeight is not null,
        MarineParams.WavePeriod => WavePeriod is not null,
        MarineParams.WaveDirection => WaveDirection is not null,
        MarineParams.WaterTemperature => WaterTemperature is not null,
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };
}
