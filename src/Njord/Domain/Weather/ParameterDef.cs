using Newtonsoft.Json;

namespace Njord.Domain.Weather;

public enum ParameterGroup { Weather, Solar, Soil }

public enum ParameterGranularity { Hourly, Daily }

public enum ParameterValueType { Numeric, TimeString }

public sealed record ParameterDef(
    [property: JsonProperty("apiName")] string ApiName,
    [property: JsonProperty("unit")] string Unit,
    [property: JsonProperty("deviceClass")] string? DeviceClass,
    [property: JsonProperty("jsonKey")] string JsonKey,
    [property: JsonProperty("group")] ParameterGroup Group,
    [property: JsonProperty("granularity")] ParameterGranularity Granularity,
    [property: JsonProperty("valueType")] ParameterValueType ValueType = ParameterValueType.Numeric)
{
    public bool Equals(ParameterDef? other) => other is not null && ApiName == other.ApiName && Granularity == other.Granularity;
    public override int GetHashCode() => HashCode.Combine(ApiName, Granularity);
}
