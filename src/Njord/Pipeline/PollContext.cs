using Njord.Alerts;
using Njord.Compute.Analysis;
using Njord.Derived;
using Njord.History;
using Njord.Indices;
using Njord.Sensors;
using Njord.Trends;
using Njord.Weather;

namespace Njord.Pipeline;

public sealed record PollContext(
    ConsensusSnapshot Consensus,
    ConsensusSnapshot? Previous,
    SensorSnapshot? Sensors,
    ModelSnapshot Snapshot,
    string Location,
    IReadOnlyList<int> Horizons) : IAlertInput, IDerivedInput, ITrendInput, IIndexInput, IHistoryInput;
