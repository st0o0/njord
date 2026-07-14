namespace Njord.Domain.Weather;

public enum ParameterGroup { Weather, Solar, Soil }

public enum ParameterGranularity { Hourly, Daily }

public enum ParameterValueType { Numeric, TimeString }

public sealed record ParameterDef(
    string ApiName,
    string Unit,
    string? DeviceClass,
    string JsonKey,
    ParameterGroup Group,
    ParameterGranularity Granularity,
    ParameterValueType ValueType = ParameterValueType.Numeric)
{
    public bool Equals(ParameterDef? other) => other is not null && ApiName == other.ApiName && Granularity == other.Granularity;
    public override int GetHashCode() => HashCode.Combine(ApiName, Granularity);
}
