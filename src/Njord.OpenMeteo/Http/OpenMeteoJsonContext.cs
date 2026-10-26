using System.Text.Json.Serialization;
using Njord.OpenMeteo.AirQuality;
using Njord.OpenMeteo.Flood;
using Njord.OpenMeteo.Marine;
using Njord.OpenMeteo.Satellite;
using Njord.OpenMeteo.Seasonal;
using Njord.OpenMeteo.Weather;

namespace Njord.OpenMeteo.Http;

[JsonSerializable(typeof(WeatherResponse))]
[JsonSerializable(typeof(WeatherHourlyData))]
[JsonSerializable(typeof(AirQualityResponse))]
[JsonSerializable(typeof(FloodResponse))]
[JsonSerializable(typeof(MarineResponse))]
[JsonSerializable(typeof(SeasonalResponse))]
[JsonSerializable(typeof(SatelliteRadiationResponse))]
internal sealed partial class OpenMeteoJsonContext : JsonSerializerContext;
