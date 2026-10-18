using Njord.Weather;

namespace Njord.Messages.Derived;

public sealed record HorizonDerived(
    int? Beaufort,
    double? WindChill,
    DewPointComfort? DewPointComfort,
    WeatherCondition? Condition);
