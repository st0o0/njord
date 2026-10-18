using Njord.Consensus;
using Njord.Model;

namespace Njord.Consensus.Querying;

public readonly record struct HorizonWindow(ParameterConsensus Series, Horizon Last)
{
    public IEnumerable<(Horizon Horizon, HorizonConsensus Consensus)> Entries
    {
        get
        {
            var last = Last;
            return Series.ByHorizon
                .Where(e => e.Key.Kind == last.Kind && e.Key.Offset <= last.Offset)
                .Select(e => (e.Key, e.Value));
        }
    }
}
