namespace Njord.Compute.Contracts;

public sealed record HorizonDerived(
    int? Beaufort,
    double? WindChill,
    DewPointComfort? DewPointComfort,
    WeatherCondition? Condition);
