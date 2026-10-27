namespace Njord.Core.Flood;

public sealed record FloodPoint(
    DateTimeOffset ValidFrom,
    double? Discharge = null,
    double? DischargeMean = null,
    double? DischargeMedian = null,
    double? DischargeMax = null,
    double? DischargeMin = null,
    double? DischargeP25 = null,
    double? DischargeP75 = null,
    IReadOnlyList<double>? EnsembleMembers = null) : IHasValidTime
{
    private static readonly FloodParams[] Flags =
        [.. Enum.GetValues<FloodParams>().Where(p => p != FloodParams.None)];

    public bool HasFields(FloodParams required) =>
        Flags.All(flag => !required.HasFlag(flag) || Has(flag));

    private bool Has(FloodParams param) => param switch
    {
        FloodParams.Discharge => Discharge is not null,
        FloodParams.DischargeMean => DischargeMean is not null,
        FloodParams.DischargeMedian => DischargeMedian is not null,
        FloodParams.DischargeMax => DischargeMax is not null,
        FloodParams.DischargeMin => DischargeMin is not null,
        FloodParams.DischargeP25 => DischargeP25 is not null,
        FloodParams.DischargeP75 => DischargeP75 is not null,
        FloodParams.Ensemble => EnsembleMembers is not null,
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };
}
