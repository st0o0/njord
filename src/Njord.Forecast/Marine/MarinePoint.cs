namespace Njord.Forecast.Marine;

public sealed record MarinePoint(
    DateTimeOffset ValidFrom,
    double? WaveHeight = null,
    double? WavePeriod = null,
    double? WaveDirection = null,
    double? WaterTemperature = null) : IHasValidTime
{
    private static readonly MarineParam[] Flags =
        [.. Enum.GetValues<MarineParam>().Where(p => p != MarineParam.None)];

    public bool HasFields(MarineParam required) =>
        Flags.All(flag => !required.HasFlag(flag) || Has(flag));

    private bool Has(MarineParam param) => param switch
    {
        MarineParam.WaveHeight => WaveHeight is not null,
        MarineParam.WavePeriod => WavePeriod is not null,
        MarineParam.WaveDirection => WaveDirection is not null,
        MarineParam.WaterTemperature => WaterTemperature is not null,
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };
}
