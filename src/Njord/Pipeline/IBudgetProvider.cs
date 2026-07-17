using Microsoft.Extensions.Options;
using Njord.Configuration;

namespace Njord.Pipeline;

public sealed record BudgetRate(int CostPerMinute, int MaxBurst);

public interface IBudgetProvider
{
    BudgetRate GetCurrentRate();
}

public sealed class OptionsBudgetProvider : IBudgetProvider
{
    private readonly IOptionsMonitor<NjordOptions> _options;

    public OptionsBudgetProvider(IOptionsMonitor<NjordOptions> options)
    {
        _options = options;
    }

    public BudgetRate GetCurrentRate()
    {
        var budget = _options.CurrentValue.EffectiveBudget;
        return new BudgetRate((int)(budget.RequestsPerMinute * 0.8), 4);
    }
}
