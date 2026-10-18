using Njord.Messages.Indices;
using Njord.Model;

namespace Njord.Indices;

public sealed record DaySlice(
    int DayOffset,
    PeriodMeans Means,
    int DaylightHoursCount,
    int NighttimeHoursCount,
    double? SunshinePercent,
    ConfidenceBounds? Bounds)
{
    public IReadOnlyDictionary<ParameterDef, double?> DayMeans => Means.Daylight;

    public IReadOnlyDictionary<ParameterDef, double?> NightMeans => Means.Night;

    public IReadOnlyDictionary<ParameterDef, double?> FullDayMeans => Means.FullDay;

    public int HoursIncluded => Math.Max(DaylightHoursCount, NighttimeHoursCount);

    public ScoredIndex Score(DayPeriod period, Func<ScoreInputs, int> formula)
    {
        var value = formula(ScoreInputs.From(Means.For(period), SunshinePercent));
        if (Bounds is not { } bounds)
        {
            return new ScoredIndex(value, null);
        }

        var pessimistic = formula(ScoreInputs.From(bounds.Pessimistic.For(period), null));
        var optimistic = formula(ScoreInputs.From(bounds.Optimistic.For(period), null));
        var envelope = new ScoreEnvelope(
            Math.Min(pessimistic, optimistic), Math.Max(pessimistic, optimistic), bounds.Agreement);
        return new ScoredIndex(value, envelope);
    }
}
