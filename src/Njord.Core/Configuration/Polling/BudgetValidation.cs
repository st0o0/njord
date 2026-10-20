using Njord.Core.Domain;
using Njord.Forecast;

namespace Njord.Core.Configuration.Polling;

public sealed record BudgetValidation(
    long ProjectedMonthlyCalls,
    long MonthlyLimit,
    double UsagePercent,
    bool WithinBudget,
    IReadOnlyList<string> Warnings,
    int DomainCallsPerCycle,
    int ApiCallWeight);
