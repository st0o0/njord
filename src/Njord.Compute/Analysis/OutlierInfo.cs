using Newtonsoft.Json;
using Njord.Weather;

namespace Njord.Compute.Analysis;

public sealed record OutlierInfo(
    [property: JsonProperty("model")] WeatherModel Model,
    [property: JsonProperty("deviation")] double Deviation);
