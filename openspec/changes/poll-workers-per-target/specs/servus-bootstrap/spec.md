## MODIFIED Requirements

### Requirement: Top-level actors are cluster singletons registered per feature
Every long-lived domain actor SHALL be registered as a cluster singleton with `WithSingleton<TMarker>` inside its feature's `IActorRegistration`; `ConfigManager` is the one exception and SHALL be created with `WithActors` and registered in the `ActorRegistry` under `IConfigManager`. The only shard region SHALL be the poll shard region (`WithShardRegion<IPollRegion>`, region name `poll-worker`) of per-target `PollWorker` entities, orchestrated by the `PollManager` singleton.

#### Scenario: Actor names are preserved
- **WHEN** actors are registered via feature `IActorRegistration` implementations
- **THEN** they use the singleton names `poll-manager`, `pipeline`, `forecast-read-model`, `gateway-supervisor`, `budget-tracker-supervisor`, `sensor-hub`, `mqtt-connection` and `mqtt-state`, the shard region name `poll-worker`, and `ConfigManager` is named `config-manager`
