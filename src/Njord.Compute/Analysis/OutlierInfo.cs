using Njord.Weather;

namespace Njord.Compute.Analysis;

public sealed record OutlierInfo(
    WeatherModel Model,
    double Deviation);
