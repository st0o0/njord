## Why

Adding a new enrichment type today requires editing 8+ files in lockstep
(EnrichmentOptions, EnrichmentActor, DiscoveryActor, DiscoveryPayloadBuilder,
EgressEvent, MqttEgressActor, StatePayloadBuilder, TopicScheme) with no
compiler enforcement — miss one and the result is a silent bug. The repeated
if-chains, duplicated device-envelope JSON, and parameter-threading across
Build\* methods make the enrichment surface the single largest maintainability
risk in the codebase. This change introduces a type system that makes each
enrichment type self-contained and registered via DI, so adding a new type
means implementing one class.

## What Changes

- **Enrichment type hierarchy** — introduce `IEnrichmentFeature` (base),
  `IStatelessEnrichment<TResult>` (5 types: Consensus, Alerts, Derived,
  Indices, Energy), `IStatefulEnrichment<TResult>` (Trends — needs previous
  snapshot), and `IActorEnrichment` (History — needs child actors).
- **Self-contained feature classes** — each enrichment type becomes a DI-
  registered class that owns its compute logic delegation, discovery payload
  building, state message mapping, device ID, and topic scheme. Dependencies
  (parameters, horizons, options) are constructor-injected, not threaded
  through method parameters.
- **DiscoveryContext record** — replaces the `(mqtt, pollInterval, version)`
  triplet that is currently passed to every Build\* method.
- **Device envelope extraction** — the 8 copies of HA device JSON boilerplate
  in DiscoveryPayloadBuilder become a single shared helper.
- **EnrichmentActor simplification** — 7 Materialize\* methods become a loop
  over `IEnumerable<IEnrichmentFeature>`.
- **DiscoveryActor simplification** — 7 if-blocks become a loop over enabled
  features.
- **MqttEgressActor dispatch** — 7-arm switch becomes registry-based dispatch
  via `IEnrichmentFeature.ToStateMessages()`.
- **EgressEvent simplification** — 7 concrete enrichment records become a
  single `EnrichmentUpdate(string Location, string TypeName, object Result)`.
  `PerModelUpdate` stays as-is (it is not an enrichment).
- **TopicScheme simplification** — 14 type-specific methods reduce to
  parameterised `DeviceId(location, typeName)` and
  `EnrichmentTopic(baseTopic, location, typeName)`.
- **Bug fix:** `ForecastHistoryActor` uses `DateTimeOffset.UtcNow` directly —
  inject `TimeProvider` for consistency and testability.
- **Bug fix:** `EnrichmentActor.MaterializeHistoryConsumer` blocks the stream
  thread with `.Result` on an `Ask` — the `IActorEnrichment.Materialize`
  pattern gives History full control to use `SelectAsync` instead.

## Non-goals

- Changing the compute logic itself (AlertEvaluator, IndexScorer,
  ConsensusComputer, etc.) — the domain algorithms stay as-is.
- Replacing parameter string lookups with typed access — that is a separate
  change.
- Fixing domain-to-configuration coupling — that is a separate change.
- Changing the per-model forecast pipeline (`PerModelUpdate` is not an
  enrichment and stays unchanged).
- API-budget impact: zero — this change is purely structural, no additional
  HTTP requests.

## Capabilities

### New Capabilities

- `enrichment-feature-registry`: The `IEnrichmentFeature` type hierarchy,
  DI registration pattern, `DiscoveryContext` record, and the loop-based
  dispatch in EnrichmentActor, DiscoveryActor, and MqttEgressActor.

### Modified Capabilities

- `enrichment-actor`: Consumer streams are materialised by looping over
  registered `IEnrichmentFeature` instances instead of hard-coded if-chains
  and Materialize\* methods. History uses `SelectAsync` instead of blocking
  `.Result`.
- `egress-event`: The 7 enrichment-specific record variants are replaced by
  a single `EnrichmentUpdate` variant. `PerModelUpdate` is unchanged.

## Impact

- **New files:** `IEnrichmentFeature.cs`, `IStatelessEnrichment.cs`,
  `IStatefulEnrichment.cs`, `IActorEnrichment.cs`, `DiscoveryContext.cs`,
  7 feature classes (`ConsensusEnrichment`, `AlertEnrichment`,
  `DerivedEnrichment`, `TrendEnrichment`, `IndexEnrichment`,
  `EnergyEnrichment`, `HistoryEnrichment`).
- **Heavily modified:** `EnrichmentActor.cs`, `DiscoveryActor.cs`,
  `MqttEgressActor.cs`, `EgressEvent.cs`, `DiscoveryPayloadBuilder.cs`,
  `StatePayloadBuilder.cs`, `TopicScheme.cs`, `NjordServiceSetup.cs`,
  `ForecastHistoryActor.cs`.
- **Tests:** Existing tests for all modified files need updates. New tests
  for feature classes and the registry dispatch.
- **No new packages.**
