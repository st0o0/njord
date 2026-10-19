namespace Njord.Model;

public sealed record FloodPoint(
    DateTimeOffset ValidFrom,
    double Discharge,
    double? DischargeP10 = null,
    double? DischargeP90 = null,
    double? MedianHistorical = null) : IHasValidTime;
