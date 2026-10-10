using Akka.Streams;
using Njord.Messages.Egress;
using Njord.Messages.Pipeline;
using Njord.Weather;

namespace Njord.Core.Actors;

// Pipeline stream wiring (Phase 2 will remove these)
public sealed record RequestPipelineSink(long RequestId);
public sealed record PipelineSinkResponse(long RequestId, ISinkRef<WeightedTarget> SinkRef);
public sealed record PipelineSinkFailed(long RequestId, Exception Cause);
public sealed record RequestPipelineSource(long RequestId);
public sealed record PipelineSourceResponse(long RequestId, ISourceRef<FetchOutcome> SourceRef);
public sealed record PipelineSourceFailed(long RequestId, Exception Cause);
public sealed record FailureConsumerCompleted;
public sealed record FailureConsumerFailed(Exception Cause);

// ModelState stream wiring
public sealed record RequestModelStateSource(long RequestId);
public sealed record ModelStateSourceResponse(long RequestId, ISourceRef<PipelineEvent> SourceRef);
public sealed record ModelStateSourceFailed(long RequestId, Exception Cause);

// Enrichment stream wiring
public sealed record RequestEnrichmentSource(long RequestId);
public sealed record EnrichmentSourceResponse(long RequestId, ISourceRef<PipelineEvent> SourceRef);
public sealed record EnrichmentSourceFailed(long RequestId, Exception Cause);

// New pipeline output wiring (replaces ModelState + Enrichment sources)
public sealed record RequestPipelineOutput(long RequestId);
public sealed record PipelineOutputResponse(long RequestId, ISourceRef<PipelineEvent> SourceRef);
public sealed record PipelineOutputFailed(long RequestId, Exception Cause);
