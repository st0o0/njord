## Why

The codebase has 7 locations using `DateTimeOffset.UtcNow` directly instead of the injected `TimeProvider`, violating the architecture guardrail "`TimeProvider` everywhere." This makes those code paths untestable with fake time and inconsistent with the rest of the system. Additionally, 5 actors use synchronous `GetActor` in `PreStart` which is fragile when startup ordering changes, and 3 enrichment config classes lack validation for invalid values that would cause silent runtime failures.

## What Changes

- Replace all 7 `DateTimeOffset.UtcNow` usages with `TimeProvider.GetUtcNow()` in MqttConnectionActor (2×), WeatherGrpcService (4×), and HistoryAnalyzer (1×).
- Replace synchronous `IActorRegistry.GetActor` calls in `PreStart` with deferred resolution (stash until resolved, or lazy lookup) in ModelStateActor, EnrichmentActor, MqttEgressActor, DiscoveryActor, and GrpcSnapshotConsumerActor.
- Add `IValidateOptions` implementations for ConsensusOptions, EnergyOptions, and HistoryOptions to catch invalid configuration at startup.

## Non-goals

- Adding `TimeProvider` to code paths that don't currently use time at all.
- Changing actor supervision strategies or message protocols.
- Migrating away from `IActorRegistry` — only fixing the sync-in-PreStart pattern.

## Capabilities

### New Capabilities

- `enrichment-config-validation`: Startup validation for enrichment feature configuration (ConsensusOptions, EnergyOptions, HistoryOptions).

### Modified Capabilities

- `mqtt-actor-topology`: MqttConnectionActor switches from `DateTimeOffset.UtcNow` to `TimeProvider`.
- `enrichment-actor`: EnrichmentActor switches from sync GetActor in PreStart to deferred resolution.

## Impact

- **Code**: MqttConnectionActor, WeatherGrpcService, HistoryAnalyzer, ModelStateActor, EnrichmentActor, MqttEgressActor, DiscoveryActor, GrpcSnapshotConsumerActor, enrichment options classes.
- **DI**: TimeProvider must be injected where not already available; IValidateOptions registrations added.
- **Tests**: Existing tests may need TimeProvider fakes; new validation tests for config options.
- **No API budget impact** — no polling changes.
