using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.OpenMeteo;

internal sealed class OptionsBudgetProvider : IBudgetProvider
{
    private readonly IOptions<PollingOptions> _options;

    public OptionsBudgetProvider(IOptions<PollingOptions> options)
    {
        _options = options;
    }

    public BudgetRate GetCurrentRate()
    {
        var budget = _options.Value.EffectiveBudget;
        var costPerMinute = (int)(budget.RequestsPerMinute * 0.8);
        var maxBurst = Math.Max(costPerMinute / 30, 16);
        return new BudgetRate(costPerMinute, maxBurst);
    }
}
