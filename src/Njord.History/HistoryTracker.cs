using Microsoft.Extensions.Options;
using Njord.Weather;

namespace Njord.History;

public sealed class HistoryTracker
{
    private readonly HistoryComputer _computer;
    private readonly HistoryOptions _options;

    public HistoryTracker(IOptions<HistoryOptions> options, HistoryComputer computer)
    {
        _options = options.Value;
        _computer = computer;
    }

    public bool Enabled => _options.Enabled;

    public HistoryResult? Analyze(
        ForecastHistory history,
        ModelSnapshot snapshot,
        string location,
        ResolvedParameterSet parameters,
        TimeProvider timeProvider)
    {
        if (!Enabled)
            return null;

        return _computer.Compute(history, snapshot, location, parameters, timeProvider, _options);
    }
}
