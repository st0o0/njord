using System.Diagnostics.Metrics;

namespace Njord.Budget;

internal static class Telemetry
{
    public const string MeterName = "Njord.Budget";

    internal static readonly Meter Meter = new(MeterName);

    private static long _dailyUsedValue;
    private static long _monthlyUsedValue;
    private static long _dailyLimitValue;
    private static long _monthlyLimitValue;

    internal static void SetDailyUsed(long value) => Volatile.Write(ref _dailyUsedValue, value);

    internal static void SetMonthlyUsed(long value) => Volatile.Write(ref _monthlyUsedValue, value);

    internal static void SetDailyLimit(long value) => Volatile.Write(ref _dailyLimitValue, value);

    internal static void SetMonthlyLimit(long value) => Volatile.Write(ref _monthlyLimitValue, value);

    internal static readonly ObservableGauge<long> UsedDaily =
        Meter.CreateObservableGauge("njord_budget_used_daily", () => Volatile.Read(ref _dailyUsedValue), "{request}");

    internal static readonly ObservableGauge<long> UsedMonthly =
        Meter.CreateObservableGauge("njord_budget_used_monthly", () => Volatile.Read(ref _monthlyUsedValue), "{request}");

    internal static readonly ObservableGauge<long> LimitDaily =
        Meter.CreateObservableGauge("njord_budget_limit_daily", () => Volatile.Read(ref _dailyLimitValue), "{request}");

    internal static readonly ObservableGauge<long> LimitMonthly =
        Meter.CreateObservableGauge("njord_budget_limit_monthly", () => Volatile.Read(ref _monthlyLimitValue), "{request}");
}
