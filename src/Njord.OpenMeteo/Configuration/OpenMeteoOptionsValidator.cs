using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.OpenMeteo.Configuration;

public sealed class OpenMeteoOptionsValidator :
    IValidateOptions<WeatherOptions>,
    IValidateOptions<AirQualityOptions>,
    IValidateOptions<FloodOptions>,
    IValidateOptions<MarineOptions>,
    IValidateOptions<SeasonalOptions>,
    IValidateOptions<SatelliteOptions>,
    IValidateOptions<SolarYieldOptions>
{
    public ValidateOptionsResult Validate(string? name, WeatherOptions options) =>
        Result(Range("Weather.ForecastDays", options.ForecastDays, 1, 16));

    public ValidateOptionsResult Validate(string? name, AirQualityOptions options) =>
        Result(
            Range("AirQuality.ForecastDays", options.ForecastDays, 1, 7),
            Enum.IsDefined(options.Region)
                ? null
                : $"AirQuality.Region '{options.Region}' is not a valid AirQualityRegion.");

    public ValidateOptionsResult Validate(string? name, FloodOptions options) =>
        Result(Range("Flood.ForecastDays", options.ForecastDays, 1, 210));

    public ValidateOptionsResult Validate(string? name, MarineOptions options) =>
        Result(Range("Marine.ForecastDays", options.ForecastDays, 1, 8));

    public ValidateOptionsResult Validate(string? name, SeasonalOptions options) =>
        Result(Range("Seasonal.ForecastMonths", options.ForecastMonths, 1, 6));

    public ValidateOptionsResult Validate(string? name, SatelliteOptions options) =>
        Result(Range("Satellite.PastHours", options.PastHours, 1, 48));

    public ValidateOptionsResult Validate(string? name, SolarYieldOptions options) =>
        Result(
            options.PeakKw > 0 ? null : $"SolarYield.PeakKw must be positive, got {options.PeakKw}",
            Range("SolarYield.Azimuth", options.Azimuth, 0, 360),
            Range("SolarYield.Tilt", options.Tilt, 0, 90));

    private static string? Range(string path, double value, double min, double max) =>
        value >= min && value <= max ? null : $"{path} must be between {min} and {max}, got {value}";

    private static ValidateOptionsResult Result(params string?[] failures) =>
        failures.OfType<string>().ToList() is { Count: > 0 } failed
            ? ValidateOptionsResult.Fail(failed)
            : ValidateOptionsResult.Success;
}
