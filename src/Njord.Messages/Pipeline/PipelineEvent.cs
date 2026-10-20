using System.Collections.Immutable;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Flood;
using Njord.Forecast.Marine;
using Njord.Forecast.Seasonal;
using Njord.Forecast.Solar;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;

namespace Njord.Messages.Pipeline;

public abstract record PipelineEvent
{
    public sealed record WeatherDataReceived(
        Location Location,
        DomainForecast<WeatherPoint> Forecast) : PipelineEvent;

    public sealed record AirQualityDataReceived(
        Location Location,
        DomainForecast<AirQualityPoint> Forecast) : PipelineEvent;

    public sealed record FloodDataReceived(
        Location Location,
        DomainForecast<FloodPoint> Forecast) : PipelineEvent;

    public sealed record SolarDataReceived(
        Location Location,
        DomainForecast<SolarPoint> Forecast) : PipelineEvent;

    public sealed record MarineDataReceived(
        Location Location,
        DomainForecast<MarinePoint> Forecast) : PipelineEvent;

    public sealed record SeasonalDataReceived(
        Location Location,
        DomainForecast<SeasonalPoint> Forecast) : PipelineEvent;

    public sealed record SatelliteDataReceived(
        Location Location,
        DomainForecast<SatelliteRadiationPoint> Forecast) : PipelineEvent;

    public sealed record AlertRaised(
        Location Location,
        Alert Alert,
        DateTimeOffset Timestamp) : PipelineEvent
    {
        public AlertType Type => Alert.Type;

        public AlertSeverity Severity => Alert.Severity;

        public ConfidenceLevel Confidence => Alert.Level;

        public AlertDetails Details => Alert.Details;
    }

    public sealed record VentilationAdvised(
        Location Location,
        DateTimeOffset BestStart,
        DateTimeOffset BestEnd,
        string Reason) : PipelineEvent;

    public sealed record ExerciseAdvised(
        Location Location,
        string Level,
        string[] Reasons,
        DateTimeOffset BestWindow) : PipelineEvent;

    public sealed record LaundryScoreComputed(
        Location Location,
        int Score,
        DateTimeOffset BestStart,
        int BestHours) : PipelineEvent;

    public sealed record CommuteSafety(
        Location Location,
        string Level,
        string[] Risks) : PipelineEvent;

    public sealed record PollenTrendComputed(
        Location Location,
        Dictionary<string, string> Trends,
        string? PeakType,
        DateTimeOffset? PeakTime) : PipelineEvent;

    public sealed record NightVentilationAdvised(
        Location Location,
        DateTimeOffset OpenAt,
        DateTimeOffset CloseAt,
        double LowTemp,
        double TomorrowPeak) : PipelineEvent;

    public sealed record ConfidenceTrend(
        Location Location,
        string Direction,
        double CurrentAvgStdDev,
        double PreviousAvgStdDev) : PipelineEvent;

    public sealed record SolarYieldComputed(
        Location Location,
        double KwhPerKwp,
        int PeakHour) : PipelineEvent;

    public sealed record GardeningAdvised(
        Location Location,
        bool PlantingSafe,
        bool WateringNeeded,
        bool FrostRisk) : PipelineEvent;

    public sealed record OutdoorScoreComputed(
        Location Location,
        int Score,
        DateTimeOffset BestStart,
        int BestHours) : PipelineEvent;

    public sealed record BBQScoreComputed(
        Location Location,
        int Score,
        DateTimeOffset BestStart,
        int BestHours) : PipelineEvent;

    public sealed record FloodTimelineComputed(
        Location Location,
        DateTimeOffset PeakTime,
        double PeakDischarge,
        int DaysAboveWarning) : PipelineEvent;

    public sealed record FrontApproaching(
        Location Location,
        TimeSpan EstimatedIn,
        double PressureDrop,
        double TempDrop) : PipelineEvent;

    public sealed record SurfConditionsComputed(
        Location Location,
        int Score,
        string Rating,
        double SwellHeight,
        double SwellPeriod,
        string? WindRelation,
        DateTimeOffset BestStart) : PipelineEvent;

    // Irradiance in W/m2 of the newest observed hour; BlendedKwhPerM2 is the next 24 h (observed hours from the
    // satellite, later hours from the ensemble) and null while no ensemble tilted irradiance is known.
    public sealed record SatelliteRadiationComputed(
        Location Location,
        DateTimeOffset ObservedAt,
        double GlobalTilted,
        double? GlobalHorizontal,
        double? BlendedKwhPerM2) : PipelineEvent;

    public sealed record SeasonalOutlookComputed(
        Location Location,
        ImmutableArray<MonthlyOutlook> Months) : PipelineEvent;
}
