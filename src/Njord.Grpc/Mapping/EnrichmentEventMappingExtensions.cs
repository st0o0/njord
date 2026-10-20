using Google.Protobuf.WellKnownTypes;
using Njord.Forecast;
using Njord.Grpc.V2;
using Njord.Messages.Pipeline;

namespace Njord.Grpc.Mapping;

internal static class EnrichmentEventMappingExtensions
{
    public static EnrichmentEvent? ToEnrichmentEvent(this PipelineEvent pipelineEvent, DateTimeOffset now) => pipelineEvent switch
    {
        FrostAlertResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            Frost = r.ToProto(),
        },
        HeatAlertResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            Heat = r.ToProto(),
        },
        StormAlertResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            Storm = r.ToProto(),
        },
        HeavyRainAlertResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            HeavyRain = r.ToProto(),
        },
        UvAlertResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            Uv = r.ToProto(),
        },
        FogAlertResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            Fog = r.ToProto(),
        },
        SnowAlertResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            Snow = r.ToProto(),
        },
        PressureDropAlertResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            PressureDrop = r.ToProto(),
        },
        ThunderstormAlertResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            Thunderstorm = r.ToProto(),
        },
        IceAlertResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            Ice = r.ToProto(),
        },
        WindChillAlertResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            WindChill = r.ToProto(),
        },
        VisibilityAlertResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            Visibility = r.ToProto(),
        },
        TropicalNightAlertResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            TropicalNight = r.ToProto(),
        },
        HumidityAlertResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            Humidity = r.ToProto(),
        },
        PollenAlertResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            Pollen = r.ToProto(),
        },
        Pm25AlertResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            Pm25 = r.ToProto(),
        },
        DustAlertResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            Dust = r.ToProto(),
        },
        FloodRiskAlertResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            FloodRisk = r.ToProto(),
        },
        FloodRainAlertResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            FloodRain = r.ToProto(),
        },
        SwimmingConditionsAlertResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            SwimmingConditions = r.ToProto(),
        },
        BbqScoreResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            Bbq = r.ToProto(),
        },
        LaundryScoreResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            Laundry = r.ToProto(),
        },
        OutdoorScoreResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            Outdoor = r.ToProto(),
        },
        SurfConditionsResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            SurfConditions = r.ToProto(),
        },
        CommuteSafetyResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            CommuteSafety = r.ToProto(),
        },
        VentilationWindowResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            VentilationWindow = r.ToProto(),
        },
        NightVentilationResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            NightVentilation = r.ToProto(),
        },
        ExerciseAdvisoryResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            ExerciseAdvisory = r.ToProto(),
        },
        GardeningResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            Gardening = r.ToProto(),
        },
        FloodTimelineResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            FloodTimeline = r.ToProto(),
        },
        SolarYieldResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            SolarYield = r.ToProto(),
        },
        SatelliteRadiationResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            SatelliteRadiation = r.ToProto(),
        },
        ApproachingFrontResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            ApproachingFront = r.ToProto(),
        },
        ConfidenceTrendResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            ConfidenceTrend = r.ToProto(),
        },
        PollenTrendResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            PollenTrend = r.ToProto(),
        },
        SeasonalOutlookResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            UpdatedAt = now.ToTimestamp(),
            SeasonalOutlook = r.ToProto(),
        },
        FeatureResult r => throw new ArgumentOutOfRangeException(nameof(pipelineEvent), r.GetType().Name, "Unmapped feature result"),
        _ => null,
    };

    public static Location? ToEnrichmentLocation(this PipelineEvent pipelineEvent) =>
        (pipelineEvent as FeatureResult)?.Location;

    public static GetEnrichmentsResponse ToProto(this LatestAnalysisResponse response, DateTimeOffset now)
    {
        switch (response)
        {
            case LatestAnalysisNotAvailable missing:
                return new GetEnrichmentsResponse { Location = missing.Location.Name };
            case LatestAnalysisResult found:
                var proto = new GetEnrichmentsResponse { Location = found.Location.Name };
                proto.Events.AddRange(found.Results.Select(result => result.ToEnrichmentEvent(now)).OfType<EnrichmentEvent>());
                return proto;
            default:
                throw new ArgumentOutOfRangeException(nameof(response), response.GetType().Name, "Unknown analysis response");
        }
    }
}
