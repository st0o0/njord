## REMOVED Requirements

### Requirement: The EnrichmentActor requests a SourceRef from the PipelineActor
**Reason**: `EnrichmentActor`, its consumer streams, `EgressActor` and `ForecastHistoryActor` were removed. Consensus and all analysis features run inside `ForecastPipeline`'s `StatefulSelectMany` stage (src/Njord.Pipeline/ForecastPipeline.cs).
**Migration**: See `pipeline-composition` for the analysis stream, sensor side channel and settings side channel.

### Requirement: EnrichmentActor resolves SensorHub dependency
**Reason**: `EnrichmentActor`, its consumer streams, `EgressActor` and `ForecastHistoryActor` were removed. Consensus and all analysis features run inside `ForecastPipeline`'s `StatefulSelectMany` stage (src/Njord.Pipeline/ForecastPipeline.cs).
**Migration**: See `pipeline-composition` for the analysis stream, sensor side channel and settings side channel.

### Requirement: EnrichmentActor pulls SensorSnapshot per location
**Reason**: `EnrichmentActor`, its consumer streams, `EgressActor` and `ForecastHistoryActor` were removed. Consensus and all analysis features run inside `ForecastPipeline`'s `StatefulSelectMany` stage (src/Njord.Pipeline/ForecastPipeline.cs).
**Migration**: See `pipeline-composition` for the analysis stream, sensor side channel and settings side channel.

### Requirement: The EnrichmentActor maintains a ModelSnapshot via Scan
**Reason**: `EnrichmentActor`, its consumer streams, `EgressActor` and `ForecastHistoryActor` were removed. Consensus and all analysis features run inside `ForecastPipeline`'s `StatefulSelectMany` stage (src/Njord.Pipeline/ForecastPipeline.cs).
**Migration**: See `pipeline-composition` for the analysis stream, sensor side channel and settings side channel.

### Requirement: The EnrichmentActor fans out via a second BroadcastHub
**Reason**: `EnrichmentActor`, its consumer streams, `EgressActor` and `ForecastHistoryActor` were removed. Consensus and all analysis features run inside `ForecastPipeline`'s `StatefulSelectMany` stage (src/Njord.Pipeline/ForecastPipeline.cs).
**Migration**: See `pipeline-composition` for the analysis stream, sensor side channel and settings side channel.

### Requirement: EnrichmentActor fans out enrichment results to EgressActor
**Reason**: `EnrichmentActor`, its consumer streams, `EgressActor` and `ForecastHistoryActor` were removed. Consensus and all analysis features run inside `ForecastPipeline`'s `StatefulSelectMany` stage (src/Njord.Pipeline/ForecastPipeline.cs).
**Migration**: See `pipeline-composition` for the analysis stream, sensor side channel and settings side channel.

### Requirement: The EnrichmentActor pipeline broadcasts ModelSnapshot before consensus
**Reason**: `EnrichmentActor`, its consumer streams, `EgressActor` and `ForecastHistoryActor` were removed. Consensus and all analysis features run inside `ForecastPipeline`'s `StatefulSelectMany` stage (src/Njord.Pipeline/ForecastPipeline.cs).
**Migration**: See `pipeline-composition` for the analysis stream, sensor side channel and settings side channel.

### Requirement: Consumer streams are materialized only when enabled
**Reason**: `EnrichmentActor`, its consumer streams, `EgressActor` and `ForecastHistoryActor` were removed. Consensus and all analysis features run inside `ForecastPipeline`'s `StatefulSelectMany` stage (src/Njord.Pipeline/ForecastPipeline.cs).
**Migration**: See `pipeline-composition` for the analysis stream, sensor side channel and settings side channel.

### Requirement: Consensus egress events originate from the consensus stage
**Reason**: `EnrichmentActor`, its consumer streams, `EgressActor` and `ForecastHistoryActor` were removed. Consensus and all analysis features run inside `ForecastPipeline`'s `StatefulSelectMany` stage (src/Njord.Pipeline/ForecastPipeline.cs).
**Migration**: See `pipeline-composition` for the analysis stream, sensor side channel and settings side channel.

### Requirement: Enrichment streams sink to EgressActor instead of MergeHub
**Reason**: `EnrichmentActor`, its consumer streams, `EgressActor` and `ForecastHistoryActor` were removed. Consensus and all analysis features run inside `ForecastPipeline`'s `StatefulSelectMany` stage (src/Njord.Pipeline/ForecastPipeline.cs).
**Migration**: See `pipeline-composition` for the analysis stream, sensor side channel and settings side channel.

### Requirement: Stream supervision resumes on consumer errors
**Reason**: `EnrichmentActor`, its consumer streams, `EgressActor` and `ForecastHistoryActor` were removed. Consensus and all analysis features run inside `ForecastPipeline`'s `StatefulSelectMany` stage (src/Njord.Pipeline/ForecastPipeline.cs).
**Migration**: See `pipeline-composition` for the analysis stream, sensor side channel and settings side channel.

### Requirement: The EnrichmentActor materializes an alert consumer stream when enabled
**Reason**: `EnrichmentActor`, its consumer streams, `EgressActor` and `ForecastHistoryActor` were removed. Consensus and all analysis features run inside `ForecastPipeline`'s `StatefulSelectMany` stage (src/Njord.Pipeline/ForecastPipeline.cs).
**Migration**: See `pipeline-composition` for the analysis stream, sensor side channel and settings side channel.

### Requirement: The EnrichmentActor materializes a derived consumer stream when enabled
**Reason**: `EnrichmentActor`, its consumer streams, `EgressActor` and `ForecastHistoryActor` were removed. Consensus and all analysis features run inside `ForecastPipeline`'s `StatefulSelectMany` stage (src/Njord.Pipeline/ForecastPipeline.cs).
**Migration**: See `pipeline-composition` for the analysis stream, sensor side channel and settings side channel.

### Requirement: The EnrichmentActor materializes a trend consumer stream when enabled
**Reason**: `EnrichmentActor`, its consumer streams, `EgressActor` and `ForecastHistoryActor` were removed. Consensus and all analysis features run inside `ForecastPipeline`'s `StatefulSelectMany` stage (src/Njord.Pipeline/ForecastPipeline.cs).
**Migration**: See `pipeline-composition` for the analysis stream, sensor side channel and settings side channel.

### Requirement: The EnrichmentActor materializes an index consumer stream when enabled
**Reason**: `EnrichmentActor`, its consumer streams, `EgressActor` and `ForecastHistoryActor` were removed. Consensus and all analysis features run inside `ForecastPipeline`'s `StatefulSelectMany` stage (src/Njord.Pipeline/ForecastPipeline.cs).
**Migration**: See `pipeline-composition` for the analysis stream, sensor side channel and settings side channel.

### Requirement: The EnrichmentActor materializes a history consumer stream when enabled
**Reason**: `EnrichmentActor`, its consumer streams, `EgressActor` and `ForecastHistoryActor` were removed. Consensus and all analysis features run inside `ForecastPipeline`'s `StatefulSelectMany` stage (src/Njord.Pipeline/ForecastPipeline.cs).
**Migration**: See `pipeline-composition` for the analysis stream, sensor side channel and settings side channel.

### Requirement: ForecastHistoryActor uses TimeProvider
**Reason**: `EnrichmentActor`, its consumer streams, `EgressActor` and `ForecastHistoryActor` were removed. Consensus and all analysis features run inside `ForecastPipeline`'s `StatefulSelectMany` stage (src/Njord.Pipeline/ForecastPipeline.cs).
**Migration**: See `pipeline-composition` for the analysis stream, sensor side channel and settings side channel.
