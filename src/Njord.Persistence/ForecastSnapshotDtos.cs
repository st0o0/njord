using Newtonsoft.Json;

namespace Njord.Persistence;

public sealed class ForecastSnapshotDto
{
    [JsonProperty("v")] public int Version { get; set; } = 1;
    [JsonProperty("forecasts")] public Dictionary<string, ModelForecastDto> Forecasts { get; set; } = new();
}

public sealed class ModelForecastDto
{
    [JsonProperty("model")] public string ModelId { get; set; } = "";
    [JsonProperty("loc")] public string Location { get; set; } = "";
    [JsonProperty("cycle")] public long CycleUtcTicks { get; set; }
    [JsonProperty("hourly")] public ForecastPointDto[] Hourly { get; set; } = [];
    [JsonProperty("daily")] public DailyForecastPointDto[] Daily { get; set; } = [];
}

public sealed class ForecastPointDto
{
    [JsonProperty("at")] public long ValidAtUtcTicks { get; set; }
    [JsonProperty("vals")] public Dictionary<string, double?> Values { get; set; } = new();
}

public sealed class DailyForecastPointDto
{
    [JsonProperty("date")] public string Date { get; set; } = "";
    [JsonProperty("num")] public Dictionary<string, double?> NumericValues { get; set; } = new();
    [JsonProperty("meta")] public Dictionary<string, string?> MetaValues { get; set; } = new();
}
