namespace Njord.Forecast.Forecast;

public sealed record EnsemblePoint(
    DateTimeOffset ValidFrom,
    double? TemperatureSpread = null,
    double? PrecipitationSpread = null,
    double? WindSpeedSpread = null) : IHasValidTime
{
    private static readonly EnsembleParams[] Flags =
        [.. Enum.GetValues<EnsembleParams>().Where(p => p != EnsembleParams.None)];

    public bool HasFields(EnsembleParams required) =>
        Flags.All(flag => !required.HasFlag(flag) || Has(flag));

    private bool Has(EnsembleParams param) => param switch
    {
        EnsembleParams.TemperatureSpread => TemperatureSpread is not null,
        EnsembleParams.PrecipitationSpread => PrecipitationSpread is not null,
        EnsembleParams.WindSpeedSpread => WindSpeedSpread is not null,
        EnsembleParams.Models => true,
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
