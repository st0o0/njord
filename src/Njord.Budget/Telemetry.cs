using System.Diagnostics.Metrics;

namespace Njord.Budget;

internal static class Telemetry
{
    public const string MeterName = "Njord.Budget";

    internal static readonly Meter Meter = new(MeterName);

    private static long DailyUsedValue;
    private static long MonthlyUsedValue;
    private static long DailyLimitValue;
    private static long MonthlyLimitValue;

    internal static void SetDailyUsed(long value) => Volatile.Write(ref DailyUsedValue, value);

    internal static void SetMonthlyUsed(long value) => Volatile.Write(ref MonthlyUsedValue, value);

    internal static void SetDailyLimit(long value) => Volatile.Write(ref DailyLimitValue, value);

    internal static void SetMonthlyLimit(long value) => Volatile.Write(ref MonthlyLimitValue, value);

    internal static readonly ObservableGauge<long> UsedDaily =
        Meter.CreateObservableGauge("njord_budget_used_daily", () => Volatile.Read(ref DailyUsedValue), "{request}");

    internal static readonly ObservableGauge<long> UsedMonthly =
        Meter.CreateObservableGauge("njord_budget_used_monthly", () => Volatile.Read(ref MonthlyUsedValue), "{request}");

    internal static readonly ObservableGauge<long> LimitDaily =
        Meter.CreateObservableGauge("njord_budget_limit_daily", () => Volatile.Read(ref DailyLimitValue), "{request}");

    internal static readonly ObservableGauge<long> LimitMonthly =
        Meter.CreateObservableGauge("njord_budget_limit_monthly", () => Volatile.Read(ref MonthlyLimitValue), "{request}");
}
