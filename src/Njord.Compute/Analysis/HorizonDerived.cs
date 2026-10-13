namespace Njord.Compute.Analysis;

public sealed record HorizonDerived(
    int? Beaufort,
    double? WindChill,
    DewPointComfort? DewPointComfort,
    WeatherCondition? Condition);
