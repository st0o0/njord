using System.Globalization;

namespace Njord.OpenMeteo;

internal sealed class OpenMeteoRequestBuilder
{
    private readonly string _endpoint;
    private readonly List<string> _params = [];

    private OpenMeteoRequestBuilder(string endpoint)
    {
        _endpoint = endpoint;
    }

    public static OpenMeteoRequestBuilder To(string endpoint) => new(endpoint);

    public OpenMeteoRequestBuilder WithLocation(double lat, double lon)
    {
        _params.Add($"latitude={lat.ToString(CultureInfo.InvariantCulture)}");
        _params.Add($"longitude={lon.ToString(CultureInfo.InvariantCulture)}");
        return this;
    }

    public OpenMeteoRequestBuilder WithHourly(IEnumerable<string> variables)
    {
        _params.Add($"hourly={JoinList(variables)}");
        return this;
    }

    public OpenMeteoRequestBuilder WithDaily(IEnumerable<string> variables)
    {
        _params.Add($"daily={JoinList(variables)}");
        return this;
    }

    public OpenMeteoRequestBuilder WithMonthly(IEnumerable<string> variables)
    {
        _params.Add($"monthly={JoinList(variables)}");
        return this;
    }

    public OpenMeteoRequestBuilder WithForecastDays(int days)
    {
        _params.Add($"forecast_days={days.ToString(CultureInfo.InvariantCulture)}");
        return this;
    }

    public OpenMeteoRequestBuilder WithEnsemble(bool ensemble)
    {
        if (ensemble)
        {
            _params.Add("ensemble=true");
        }

        return this;
    }

    public OpenMeteoRequestBuilder With(string key, string value)
    {
        _params.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
        return this;
    }

    public string Build()
    {
        return $"{_endpoint}?{string.Join("&", [.. _params, "timeformat=unixtime", "timezone=UTC"])}";
    }

    private static string JoinList(IEnumerable<string> values) =>
        string.Join(",", values.Select(Uri.EscapeDataString));
}
