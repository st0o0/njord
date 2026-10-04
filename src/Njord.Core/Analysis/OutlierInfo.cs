using Newtonsoft.Json;
using Njord.Domain.Weather;

namespace Njord.Analysis;

public sealed record OutlierInfo(
    [property: JsonProperty("model")] WeatherModel Model,
    [property: JsonProperty("deviation")] double Deviation);
