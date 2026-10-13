using Njord.Weather;

namespace Njord.Compute.Consensus;

public sealed record OutlierInfo(
    WeatherModel Model,
    double Deviation);
