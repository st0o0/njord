## Why

Njord registers all (parameter × horizon) combinations per model via MQTT Discovery — regardless of what the model can actually deliver. This creates hundreds of permanently "Unavailable" sensors in Home Assistant: horizons beyond a model's range (e.g. icon_d2 at ~48 h has no data for +72 h), parameters a model doesn't support (e.g. icon_d2 lacks precipitation_probability), and sunrise/sunset values that are always null because `timeformat=unixtime` returns numbers but `MapDaily` casts them with `as string`. The result is a cluttered HA UI, wasted MQTT traffic, and misleading availability states.

## What Changes

- **Horizon clamping (static):** `HorizonProjection` and `DiscoveryPayloadBuilder` use `ModelCoverageRegistry.MaxForecastHours` to exclude horizons the model cannot reach. No API or config change — the data is already in the registry.
- **Parameter capability tracking (dynamic):** `ModelStateActor` learns which parameters a model actually delivers with non-null values on the first successful fetch, then notifies the discovery layer via a `ModelCapabilityLearned` message. Late-arriving parameters (null on first fetch, non-null later) trigger incremental discovery updates.
- **Deferred discovery:** `DiscoveryActor` no longer publishes discovery at startup/connect. It waits for `ModelCapabilityLearned` from every configured (location, model) pair (with a configurable timeout), then publishes only the (parameter × horizon) combinations that passed both the static horizon filter and the dynamic parameter filter. HA birth re-publishes with the same learned knowledge.
- **Null-key stripping in state payloads:** `HorizonProjection.BuildPerHorizon` removes individual null-valued parameter keys from the JSON object, producing compact payloads instead of shipping null ballast.
- **sunrise/sunset fix:** `OpenMeteoClient.MapDaily` converts unix-timestamp numbers for `TimeString` parameters to ISO 8601 datetime strings (e.g. `2026-07-15T05:23:00Z`) so they reach HA as proper timestamps instead of null.

## Non-goals

- Consensus computation — remains deferred per existing decision.
- Enrichment feature discovery — enrichment devices (alerts, trends, indices, energy, history, derived) are unaffected; they define their own component sets and are not subject to per-model capability filtering.
- Changing the poll interval, request budget, or API call weight — no new API calls are added; the same single-model requests run as before.
- Tombstoning stale discovery configs from previous runs — out of scope for this change (existing tombstone logic in `MqttConnectionActor` is unchanged).

## Capabilities

### New Capabilities

- `model-capability-tracking`: Dynamic learning of which (parameter, horizon) combinations a model actually supports, tracked in `ModelStateActor` and communicated to the discovery layer via actor messages.

### Modified Capabilities

- `mqtt-egress`: Discovery payloads are no longer a static grid from config; they are filtered by (static horizon cap ∩ dynamic parameter support). Missing values no longer surface as `unavailable` sensors — the sensors simply don't exist.
- `openmeteo-client`: `MapDaily` correctly handles `TimeString` parameters when `timeformat=unixtime` by converting unix timestamps to ISO 8601 strings.
- `mqtt-actor-topology`: `DiscoveryActor` defers initial publish until capability messages arrive (with timeout fallback) instead of publishing on first connect.
- `egress-event`: `ModelStateActor` gains capability-tracking state and emits `ModelCapabilityLearned` alongside its existing `PerModelUpdate` flow.
- `delta-publishing`: `HorizonProjection` strips individual null-valued keys from horizon JSON objects, not just skipping entire empty horizons.

## Impact

- **Actors:** `ModelStateActor` (new state + message), `DiscoveryActor` (deferred publish, capability aggregation).
- **Static helpers:** `HorizonProjection` (horizon clamping, null-key stripping), `DiscoveryPayloadBuilder` (parameter/horizon filtering).
- **Client:** `OpenMeteoClient.MapDaily` (sunrise/sunset type conversion).
- **Registry:** `ModelCoverageRegistry` (read-only usage of existing `MaxForecastHours`).
- **Tests:** Unit tests for horizon clamping, capability tracking, deferred discovery, null stripping, and sunrise/sunset conversion. Integration tests for the discovery flow with capability learning.
- **API budget:** No change — same number of API calls, same call weight. The change only reduces what gets published to MQTT, not what gets fetched.
- **Breaking for HA users:** Sensors that were permanently unavailable will disappear after upgrade. This is the intended improvement, not a regression — but users with automations referencing those entity IDs will need to update them.
