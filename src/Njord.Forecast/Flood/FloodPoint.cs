namespace Njord.Forecast.Flood;

public sealed record FloodPoint(
    DateTimeOffset ValidFrom,
    double? Discharge = null,
    double? DischargeMean = null,
    double? DischargeMedian = null,
    double? DischargeMax = null,
    double? DischargeMin = null,
    double? DischargeP25 = null,
    double? DischargeP75 = null) : IHasValidTime
{
    private static readonly FloodParam[] Flags =
        [.. Enum.GetValues<FloodParam>().Where(p => p != FloodParam.None)];

    public bool HasFields(FloodParam required) =>
        Flags.All(flag => !required.HasFlag(flag) || Has(flag));

    private bool Has(FloodParam param) => param switch
    {
        FloodParam.Discharge => Discharge is not null,
        FloodParam.DischargeMean => DischargeMean is not null,
        FloodParam.DischargeMedian => DischargeMedian is not null,
        FloodParam.DischargeMax => DischargeMax is not null,
        FloodParam.DischargeMin => DischargeMin is not null,
        FloodParam.DischargeP25 => DischargeP25 is not null,
        FloodParam.DischargeP75 => DischargeP75 is not null,
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };
}
