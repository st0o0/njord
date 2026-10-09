## Why

A cross-codebase review against the sibling FunkArr project found that `EnrichmentActor`
(384 lines) is a god-orchestrator mixing five concerns: dependency resolution, stream-graph
construction, feature filtering/classification, consensus-flow building (54-line
`BuildConsensusInlineFlow` coupling sensor polling + error handling + metrics), and metrics
recording. In the egress path, `ModelStateActor` (219 lines) embeds mutable
capability-learning (tracking known parameters, emitting `CapabilityLearned` events via
`UnionWith`/`IsSubsetOf`) as a side effect inside a stream transformation, and
`HorizonProjection.BuildPerHorizon()` conflates coordinate calculations, point lookup, and
JSON building in one method. FunkArr's equivalent (RuleSet domain) uses three focused actors
(RuleSetResolver, RuleSetManager, RuleSetWorker) where each has a single job and fails
independently.

## What Changes

- **EnrichmentActor**: extract `BuildConsensusInlineFlow` (lines ~251-305) into a separate
  `IConsensusFlowBuilder` or a named service/static factory that owns the sensor-polling,
  error-handling, and metrics aspects of the consensus computation flow. The actor keeps
  dependency resolution, feature filtering, and graph topology assembly — but no longer
  contains the inline flow logic that couples `Ask<SensorSnapshot>`, `AskTimeoutException`
  handling, and `Telemetry.Add` calls.
- **ModelStateActor**: extract the capability-learning concern (the `knownCapabilities`
  dictionary, `observedParams.IsSubsetOf(known)`, `UnionWith`, `CapabilityLearned` event
  emission — lines ~102-143) into a separate `CapabilityTracker` class or a dedicated
  child actor, so the stream transformation is a pure data-mapping step without mutable
  side effects.
- **HorizonProjection**: split `BuildPerHorizon()` into focused helper functions:
  `ToHourlyPayload(ForecastPoint, parameters)` and `ToDailyPayload(DailyPoint, parameters)`,
  with coordinate/filtering logic separate from JSON construction.

## Capabilities

### New Capabilities

- `enrichment-orchestration`: the implementation-pattern requirement that the enrichment
  pipeline's orchestration, consensus-flow construction, and capability-tracking are
  separated into focused components rather than mixed in god-actors.

### Modified Capabilities

None. Observable behavior unchanged.

## Impact

- `src/Njord.Enrichment/EnrichmentActor.cs`
- `src/Njord.Egress/ModelStateActor.cs`
- `src/Njord.Egress/HorizonProjection.cs`
- Matching tests: `src/Njord.Enrichment.Tests/`, `src/Njord.Egress.Tests/`
- No persistence/wire format changes.

## Non-goals

- Not changing which enrichment features exist or how they're registered (DI pattern stays).
- Not changing `MqttStateActor`'s dedup logic or stream-merge topology.
- Not redesigning the `IStatelessEnrichment`/`IStatefulEnrichment`/`IActorEnrichment`
  interface hierarchy — the feature-dispatch pattern itself is clean; only the orchestrator
  is too fat.

## API budget impact

None. No polling behavior changes.
