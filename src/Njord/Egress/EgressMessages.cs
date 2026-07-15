using Akka.Streams;
using Njord.Domain.Weather;

namespace Njord.Egress;

public sealed record RequestEgressSink;
public sealed record EgressSinkResponse(ISinkRef<EgressEvent> SinkRef);

public sealed record RequestEgressSource;
public sealed record EgressSourceResponse(ISourceRef<EgressEvent> SourceRef);

public sealed record ModelCapabilityLearned(
    string Location,
    WeatherModel Model,
    IReadOnlySet<ParameterDef> SupportedParameters,
    IReadOnlyList<int> ApplicableHorizons,
    IReadOnlyList<int> ApplicableDayOffsets);
