namespace Njord.Ingest;

// Field names verified against the OpenAPI spec (/v02/_doc.json, 2026-07-11).
// Unmapped response fields are intentionally ignored during deserialization.
internal sealed record AdvancedForecastResponse(
    double Lat,
    double Lon,
    string? SystemOfUnits,
    DateTimeOffset Run,
    IReadOnlyList<AdvancedForecastPoint> Data);

internal sealed record AdvancedForecastPoint(
    DateTimeOffset DateTime,
    double? Temp,
    double? PrecCurrent,
    double? WindSpeed,
    double? WindGust,
    double? Dewpoint,
    double? HumidityRelative,
    double? CloudCoverage,
    double? PressureMsl);
