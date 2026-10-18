using Njord.Messages.Alerts;
using Njord.Messages.Derived;
using Njord.Messages.Indices;
using Njord.Messages.Trends;
using Njord.Mqtt.Payloads;
using Njord.Model;

namespace Njord.Mqtt.Mapping;

internal static class AnalysisPayloadMappings
{
    private const int DerivedDigits = 1;

    public static AlertPayload ToPayload(this Alert alert)
    {
        AlertPayload payload = alert.Details switch
        {
            FrostDetails d => new FrostAlertPayload(
                d.ExpectedLow,
                d.EarliestFrost is { } earliest ? $"+{earliest.Offset}h" : null,
                d.ModelsAgreeing),
            HeatDetails d => new HeatAlertPayload(d.ExpectedMax, d.ModelsAgreeing),
            StormDetails d => new StormAlertPayload(d.ExpectedMaxGust, d.ModelsAgreeing),
            HeavyRainDetails d => new HeavyRainAlertPayload(d.HourlyExceedModels, d.DailyExceedModels),
            UvDetails d => new UvAlertPayload(d.Level, d.UvIndex),
            FogDetails d => new FogAlertPayload(d.FogHours, d.ModelsAgreeing),
            SnowDetails d => new SnowAlertPayload(d.ExpectedAccumulation, d.FreezingLevel, d.ModelsAgreeing),
            PressureDropDetails d => new PressureDropAlertPayload(d.MaxDrop, d.ModelsAgreeing),
            ThunderstormDetails d => new ThunderstormAlertPayload(d.ModelsAgreeing),
            IceDetails d => new IceAlertPayload(d.ExpectedLow, d.RainHours, d.SoilFrozen),
            WindChillDetails d => new WindChillAlertPayload(d.FeltTemperature, d.WindFactor, d.ExposureRisk),
            VisibilityDetails d => new VisibilityAlertPayload(d.MinVisibility, d.HoursBelow1000m),
            TropicalNightDetails d => new TropicalNightAlertPayload(d.NightMinimum, d.HoursAbove20),
            HumidityDetails d => new HumidityAlertPayload(d.MaxDewpoint, d.ComfortLevel),
            NoDetails => new NoDetailsAlertPayload(),
            _ => throw new ArgumentOutOfRangeException(nameof(alert), alert.Details, null),
        };

        return payload with
        {
            Severity = alert.Severity,
            Confidence = alert.Confidence,
            TriggerValue = alert.TriggerValue,
            Threshold = alert.Threshold,
            PeakValue = alert.PeakValue,
            HoursUntil = alert.HoursUntil,
            DurationHours = alert.DurationHours,
        };
    }

    public static DerivedHorizonPayload ToPayload(this HorizonDerived derived)
        => new(
            derived.Beaufort,
            Round(derived.WindChill, DerivedDigits),
            derived.DewPointComfort,
            derived.Condition?.ToDescription());

    public static DerivedMetaPayload ToPayload(this ScalarDerived scalars)
        => new(
            Round(scalars.DiurnalAmplitude, DerivedDigits),
            Round(scalars.SunshinePct, DerivedDigits),
            scalars.Inversion);

    public static TrendPayload ToPayload(this TrendResult result)
        => new(
            [.. result.ParameterTrends.Select(pair => new TrendParameterPayload(
                TrendKey.FromApiName(pair.Key), pair.Value?.Direction, pair.Value?.Delta))],
            result.WeatherChange?.ToDescription(),
            result.PrecipTiming.StartsInHours,
            result.PrecipTiming.EndsInHours,
            result.ExtremaTiming.MaxInHours,
            result.ExtremaTiming.MinInHours,
            result.Stability?.Stability,
            result.Stability?.Ratio,
            result.Decay?.DecayRate,
            result.Decay?.ReliableHours);

    public static IndexDayPayload ToPayload(this DayScoreSet day, IndexResult result)
        => new(
            [
                Score(IndexKind.Laundry, day.Laundry, day.LaundryEnvelope),
                Score(IndexKind.Outdoor, day.Outdoor, day.OutdoorEnvelope),
                Score(IndexKind.Running, day.Running, day.RunningEnvelope),
                Score(IndexKind.Cycling, day.Cycling, day.CyclingEnvelope),
                Score(IndexKind.Bbq, day.Bbq, day.BbqEnvelope),
                Score(IndexKind.Irrigation, day.Irrigation, day.IrrigationEnvelope),
                Score(IndexKind.Solar, day.Solar, day.SolarEnvelope),
                Score(IndexKind.NightVentilation, day.NightVentilation, day.NightVentilationEnvelope),
            ],
            day.HoursIncluded,
            day.DayOffset == 0
                ? new IndexTodayPayload(
                    result.FrostProtection?.HoursUntilFrost,
                    result.FrostProtection?.Confidence,
                    result.Vpd?.Category,
                    result.Vpd?.Vpd)
                : null);

    private static IndexScorePayload Score(IndexKind kind, int score, ScoreEnvelope? envelope)
        => new(kind, score, envelope is null ? null : new ScoreEnvelopePayload(envelope.Min, envelope.Max, envelope.Confidence));

    private static double? Round(double? value, int digits)
        => value is { } number ? Math.Round(number, digits) : null;
}
