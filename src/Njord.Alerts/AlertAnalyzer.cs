using Microsoft.Extensions.Options;
using Njord.Alerts.Configuration;
using Njord.Consensus;
using Njord.Messages.Alerts;

namespace Njord.Alerts;

public sealed class AlertAnalyzer
{
    private readonly IReadOnlyList<IAlertRule> _rules;

    public AlertAnalyzer(IEnumerable<IAlertRule> rules, IOptions<AlertOptions> options)
    {
        _rules = OrderedAndComplete(rules);
        Enabled = options.Value.Enabled;
    }

    public bool Enabled { get; }

    public AlertResult? Compute(LocationConsensus consensus, AlertSettings settings)
    {
        if (!Enabled)
        {
            return null;
        }

        var alerts = _rules.Select(rule => rule.Evaluate(consensus, settings)).ToList();
        return new AlertResult(consensus.Location, alerts);
    }

    private static IReadOnlyList<IAlertRule> OrderedAndComplete(IEnumerable<IAlertRule> rules)
    {
        var ordered = rules.OrderBy(rule => rule.Type).ToList();
        var counts = ordered.CountBy(rule => rule.Type).ToDictionary();
        var missing = Enum.GetValues<AlertType>().Where(type => !counts.ContainsKey(type)).ToList();
        var duplicated = counts.Where(c => c.Value > 1).Select(c => c.Key).ToList();

        if (missing.Count > 0 || duplicated.Count > 0)
        {
            throw new InvalidOperationException(
                "Every alert type needs exactly one IAlertRule. " +
                $"Missing: [{string.Join(", ", missing)}]; duplicated: [{string.Join(", ", duplicated)}].");
        }

        return ordered;
    }
}
