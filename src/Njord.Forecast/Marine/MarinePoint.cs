namespace Njord.Forecast.Marine;

public sealed record MarinePoint(
    DateTimeOffset ValidFrom,
    double? WaveHeight = null,
    double? WavePeriod = null,
    double? WaveDirection = null,
    double? WaterTemperature = null,
    double? WindWaveHeight = null,
    double? WindWavePeriod = null,
    double? WindWaveDirection = null,
    double? WindWavePeakPeriod = null,
    double? SwellWaveHeight = null,
    double? SwellWavePeriod = null,
    double? SwellWaveDirection = null,
    double? SwellWavePeakPeriod = null,
    double? SeaLevelHeight = null,
    double? OceanCurrentVelocity = null,
    double? OceanCurrentDirection = null) : IHasValidTime
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
        MarineParams.WindWaveHeight => WindWaveHeight is not null,
        MarineParams.WindWavePeriod => WindWavePeriod is not null,
        MarineParams.WindWaveDirection => WindWaveDirection is not null,
        MarineParams.WindWavePeakPeriod => WindWavePeakPeriod is not null,
        MarineParams.SwellWaveHeight => SwellWaveHeight is not null,
        MarineParams.SwellWavePeriod => SwellWavePeriod is not null,
        MarineParams.SwellWaveDirection => SwellWaveDirection is not null,
        MarineParams.SwellWavePeakPeriod => SwellWavePeakPeriod is not null,
        MarineParams.SeaLevelHeight => SeaLevelHeight is not null,
        MarineParams.OceanCurrentVelocity => OceanCurrentVelocity is not null,
        MarineParams.OceanCurrentDirection => OceanCurrentDirection is not null,
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };
}
