## REMOVED Requirements

### Requirement: EgressEvent is a protocol-neutral discriminated union
**Reason**: The egress layer no longer exists. `EgressEvent`, `EgressActor` (MergeHub/BroadcastHub), `ModelStateActor`, `PerModelUpdate` and the `Njord.Egress` project were removed; `ForecastPipeline` (src/Njord.Pipeline/ForecastPipeline.cs) now emits typed `PipelineEvent` variants (src/Njord.Messages/Pipeline/PipelineEvent.cs) into its own `BroadcastHub<PipelineEvent>`.
**Migration**: See the `pipeline-composition` capability for the analysis stream and `PipelineEvent`, and `consumer-model` for how the MQTT state publisher and `ForecastReadModel` attach via `SourceRef<PipelineEvent>`.

### Requirement: EgressActor materializes a MergeHub and BroadcastHub for EgressEvent
**Reason**: The egress layer no longer exists. `EgressEvent`, `EgressActor` (MergeHub/BroadcastHub), `ModelStateActor`, `PerModelUpdate` and the `Njord.Egress` project were removed; `ForecastPipeline` (src/Njord.Pipeline/ForecastPipeline.cs) now emits typed `PipelineEvent` variants (src/Njord.Messages/Pipeline/PipelineEvent.cs) into its own `BroadcastHub<PipelineEvent>`.
**Migration**: See the `pipeline-composition` capability for the analysis stream and `PipelineEvent`, and `consumer-model` for how the MQTT state publisher and `ForecastReadModel` attach via `SourceRef<PipelineEvent>`.

### Requirement: EgressActor pre-materializes hub before vending refs
**Reason**: The egress layer no longer exists. `EgressEvent`, `EgressActor` (MergeHub/BroadcastHub), `ModelStateActor`, `PerModelUpdate` and the `Njord.Egress` project were removed; `ForecastPipeline` (src/Njord.Pipeline/ForecastPipeline.cs) now emits typed `PipelineEvent` variants (src/Njord.Messages/Pipeline/PipelineEvent.cs) into its own `BroadcastHub<PipelineEvent>`.
**Migration**: See the `pipeline-composition` capability for the analysis stream and `PipelineEvent`, and `consumer-model` for how the MQTT state publisher and `ForecastReadModel` attach via `SourceRef<PipelineEvent>`.

### Requirement: ModelStateActor produces PerModelUpdate events
**Reason**: The egress layer no longer exists. `EgressEvent`, `EgressActor` (MergeHub/BroadcastHub), `ModelStateActor`, `PerModelUpdate` and the `Njord.Egress` project were removed; `ForecastPipeline` (src/Njord.Pipeline/ForecastPipeline.cs) now emits typed `PipelineEvent` variants (src/Njord.Messages/Pipeline/PipelineEvent.cs) into its own `BroadcastHub<PipelineEvent>`.
**Migration**: See the `pipeline-composition` capability for the analysis stream and `PipelineEvent`, and `consumer-model` for how the MQTT state publisher and `ForecastReadModel` attach via `SourceRef<PipelineEvent>`.
