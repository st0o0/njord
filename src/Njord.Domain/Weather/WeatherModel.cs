using System.ComponentModel;
using System.Globalization;
using Newtonsoft.Json;

namespace Njord.Domain.Weather;

/// <summary>An Open-Meteo model id (e.g. "icon_d2"). Free-form by design — the API accepts arbitrary strings.</summary>
[TypeConverter(typeof(WeatherModelTypeConverter))]
public sealed record WeatherModel
{
    [JsonProperty("id")]
    public string Id { get; }

    public WeatherModel(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id.Trim();
    }
}

internal sealed class WeatherModelTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
        value is string s ? new WeatherModel(s) : base.ConvertFrom(context, culture, value);

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) =>
        destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) =>
        destinationType == typeof(string) && value is WeatherModel m ? m.Id : base.ConvertTo(context, culture, value, destinationType);
}
