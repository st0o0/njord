# consumer-model Delta

## MODIFIED Requirements

### Requirement: Consumers match typed PipelineEvent variants
MqttStateActor and GrpcConsumerActor SHALL pattern-match on typed
`PipelineEvent` variants (`ForecastReceived`, `ConsensusComputed`,
`AlertComputed`, etc.) instead of `(TypeName, Result)` double-dispatch.
`EnrichmentProtoMapper` SHALL accept typed events instead of
`(string typeName, object result)` parameters.

#### Scenario: MqttStateActor matches typed events
- **WHEN** MqttStateActor receives `PipelineEvent.AlertComputed`
- **THEN** it accesses `event.Result` as `AlertResult` without casting

#### Scenario: GrpcConsumerActor stores typed state
- **WHEN** GrpcConsumerActor receives a `PipelineEvent`
- **THEN** it stores the typed variant, not `AnalysisResult`

#### Scenario: EnrichmentProtoMapper maps typed events
- **WHEN** `EnrichmentProtoMapper` maps a `PipelineEvent.TrendComputed`
- **THEN** it accesses `event.Result` as `TrendResult` directly

## REMOVED Requirements

### Requirement: Consumers use TypeName string dispatch
Removed. No `(TypeName, Result) switch` or `"alerts" when result is AlertResult`
patterns.

#### Scenario: removed
- **WHEN** removed
- **THEN** removed
