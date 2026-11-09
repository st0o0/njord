namespace Njord.Core.Forecast;

public sealed record EnsemblePoint(
    DateTimeOffset ValidFrom,
    double? TemperatureSpread = null,
    double? PrecipitationSpread = null,
    double? WindSpeedSpread = null,
    double? PressureMslSpread = null,
    double? CloudCoverSpread = null,
    double? WindGustsSpread = null,
    double? CapeSpread = null,
    double? SnowfallSpread = null,
    double? RainSpread = null,
    double? DewPointSpread = null,
    double? HumiditySpread = null,
    double? SurfacePressureSpread = null,
    double? WindDirectionSpread = null,
    double? WetBulbTemperatureSpread = null,
    double? WindSpeed100MSpread = null) : IHasValidTime
{
    private static readonly EnsembleParams[] _flags =
        [.. Enum.GetValues<EnsembleParams>().Where(p => p != EnsembleParams.None)];

    public bool HasFields(EnsembleParams required) =>
        _flags.All(flag => !required.HasFlag(flag) || Has(flag));

    private bool Has(EnsembleParams param) => param switch
    {
        EnsembleParams.TemperatureSpread => TemperatureSpread is not null,
        EnsembleParams.PrecipitationSpread => PrecipitationSpread is not null,
        EnsembleParams.WindSpeedSpread => WindSpeedSpread is not null,
        EnsembleParams.PressureMslSpread => PressureMslSpread is not null,
        EnsembleParams.CloudCoverSpread => CloudCoverSpread is not null,
        EnsembleParams.WindGustsSpread => WindGustsSpread is not null,
        EnsembleParams.CapeSpread => CapeSpread is not null,
        EnsembleParams.SnowfallSpread => SnowfallSpread is not null,
        EnsembleParams.RainSpread => RainSpread is not null,
        EnsembleParams.DewPointSpread => DewPointSpread is not null,
        EnsembleParams.HumiditySpread => HumiditySpread is not null,
        EnsembleParams.SurfacePressureSpread => SurfacePressureSpread is not null,
        EnsembleParams.WindDirectionSpread => WindDirectionSpread is not null,
        EnsembleParams.WetBulbTemperatureSpread => WetBulbTemperatureSpread is not null,
        EnsembleParams.WindSpeed100MSpread => WindSpeed100MSpread is not null,
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };
}

public static class EnsemblePointExtensions
{
    public static EnsemblePoint? SpreadAt(this IReadOnlyList<EnsemblePoint> points, DateTimeOffset validFrom)
    {
        if (points.Count == 0)
        {
            return null;
        }

        EnsemblePoint? closest = null;
        var minDelta = long.MaxValue;
        foreach (var point in points)
        {
            if (point.ValidFrom == validFrom)
            {
                return point;
            }

            var delta = Math.Abs((point.ValidFrom - validFrom).Ticks);
            if (delta < minDelta)
            {
                minDelta = delta;
                closest = point;
            }
        }

        return closest;
    }
}
