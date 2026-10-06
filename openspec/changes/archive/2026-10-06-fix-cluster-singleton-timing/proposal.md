## Why

njord's ClusterSingleton actors fail to start within 30 seconds in Docker deployments. All singleton proxies log warnings and the pipeline, enrichment, and scheduler actors never become reachable. This causes 9/42 E2E test failures: no Open-Meteo polling, no enrichment data, no enrichment entities in Home Assistant.

Root cause: `cluster.Join(selfAddress)` is registered as the last startup task in `WithNjordActors`, after all 13 `WithSingleton` calls. The proxies start their 30-second lookup timer immediately, but the cluster hasn't formed yet because the join happens last. FunkArr solves this by using `SeedNodes` in `WithClustering()`, which makes the cluster form during actor system startup — before any singleton proxy starts.

## What Changes

- Switch from manual `cluster.Join(selfAddress)` to `SeedNodes`-based cluster formation in `WithClustering()`, matching the proven FunkArr pattern
- Pin the remoting port from ephemeral (`Port = 0`) to a fixed port (`2552`), required for `SeedNodes`
- Remove the `WithActors` callback that did the manual join
- Move `AddStreamShutdownTask` to its own `WithActors` registration (it was co-located with the join)
- Update integration tests that configure the actor system to use the same pattern

## Non-goals

- Changing to multi-node clustering or Akka.Management
- Making the remoting port configurable via app settings (can be added later if needed)
- Changing singleton registration tiers or actor dependencies

## Capabilities

### Modified Capabilities

- `servus-bootstrap`: Cluster formation strategy changes from manual join to SeedNodes

## Impact

- `src/Njord/Configuration/NjordActorSystemSetup.cs` — main change (3-4 lines)
- Integration test infrastructure that creates actor systems — may need port/clustering config adjustments
- No API changes, no new dependencies, no breaking changes
- API budget: no impact on polling (this fix enables polling that was broken)
