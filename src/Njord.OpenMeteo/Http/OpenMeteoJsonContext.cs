using System.Text.Json.Serialization;
using Njord.OpenMeteo.AirQuality;
using Njord.OpenMeteo.Flood;
using Njord.OpenMeteo.Forecast;
using Njord.OpenMeteo.Marine;
using Njord.OpenMeteo.Satellite;
using Njord.OpenMeteo.Seasonal;

namespace Njord.OpenMeteo.Http;

[JsonSerializable(typeof(ForecastResponse))]
[JsonSerializable(typeof(ForecastHourlyData))]
[JsonSerializable(typeof(AirQualityResponse))]
[JsonSerializable(typeof(FloodResponse))]
[JsonSerializable(typeof(MarineResponse))]
[JsonSerializable(typeof(SeasonalResponse))]
[JsonSerializable(typeof(SatelliteRadiationResponse))]
internal sealed partial class OpenMeteoJsonContext : JsonSerializerContext;
