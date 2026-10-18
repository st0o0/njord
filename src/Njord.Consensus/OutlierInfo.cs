using Njord.Model;

namespace Njord.Consensus;

public sealed record OutlierInfo(
    WeatherModel Model,
    double Deviation);
