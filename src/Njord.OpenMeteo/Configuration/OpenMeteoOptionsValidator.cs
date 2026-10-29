using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;

namespace Njord.OpenMeteo.Configuration;

public sealed class OpenMeteoOptionsValidator :
    IValidateOptions<ForecastOptions>,
    IValidateOptions<AirQualityOptions>,
    IValidateOptions<FloodOptions>,
    IValidateOptions<MarineOptions>,
    IValidateOptions<SeasonalOptions>,
    IValidateOptions<SatelliteOptions>,
    IValidateOptions<SolarYieldOptions>
{
    public ValidateOptionsResult Validate(string? name, ForecastOptions options) =>
        Result([
            Range("Forecast.ForecastDays", options.ForecastDays, 1, 16),
            Range("Forecast.PastHours", options.PastHours, 0, 96),
            options.Models.Length == 0 ? "Forecast.Models must contain at least one model." : null,
            .. DuplicateModels(options),
            .. UnknownModels(options)]);

    private static IEnumerable<string> DuplicateModels(ForecastOptions options) =>
        options.Models
        .GroupBy(m => m).Where(g => g.Count() > 1)
        .Select(g => $"Forecast.Models contains '{g.Key}' more than once.");

    private static IEnumerable<string> UnknownModels(ForecastOptions options) =>
        options.Models
        .Where(m => WeatherModelCatalog.Find(m) is null)
        .Distinct()
        .Select(m => $"Forecast.Models entry '{m}' is not a known model (known: {string.Join(", ", WeatherModelCatalog.All.Select(i => i.Id))}).");

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
            options.SystemEfficiency > 0 && options.SystemEfficiency <= 1
                ? null
                : $"SolarYield.SystemEfficiency must be greater than 0 and at most 1, got {options.SystemEfficiency}",
            Range("SolarYield.Azimuth", options.Azimuth, 0, 360),
            Range("SolarYield.Tilt", options.Tilt, 0, 90));

    private static string? Range(string path, double value, double min, double max) =>
        value >= min && value <= max ? null : $"{path} must be between {min} and {max}, got {value}";

    private static ValidateOptionsResult Result(params string?[] failures) =>
        failures.OfType<string>().ToList() is { Count: > 0 } failed
            ? ValidateOptionsResult.Fail(failed)
            : ValidateOptionsResult.Success;
}
