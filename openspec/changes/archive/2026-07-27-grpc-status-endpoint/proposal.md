## Why

The HA plugin needs operational runtime data to show users what njord is doing:
which models are being polled, when the next poll fires, whether a model is in
discovery or steady phase, and which enrichment features are active. The existing
`GetStatus` RPC returns version, uptime, and budget aggregates but has a TODO for
per-model poll status (ConfigGrpcService.cs line 91). This change fills that gap.

No polling changes — zero API-budget impact.

## What Changes

- Extend `ServerStatus` proto message with `repeated ModelPollStatus` exposing
  per-model poll state (location, model, phase, next poll time, last data change,
  miss count, learned cycle) and `repeated string active_enrichments` listing
  enabled enrichment feature names.
- Add `GetPollStates` / `PollStatesResponse` Ask protocol to `SchedulerActor` so
  `ConfigGrpcService` can query the actor's internal `_states` dictionary — same
  pattern already used by `TriggerImmediatePoll`.
- Make `ConfigGrpcService.GetStatus` async: Ask the SchedulerActor (5s timeout),
  map `ModelPollState` to proto `ModelPollStatus`, read `EnrichmentOptions` for
  active feature names.

## Non-goals

- MQTT connection state — irrelevant to the HA plugin (internal egress detail).
- Rate-limiter token balance from `WeightedBudgetGate` — monthly/daily aggregates
  are sufficient; the gate is an internal stream detail.
- Streaming status updates — `GetStatus` is a unary poll; live status streaming
  can be added later if needed.
- Changing the existing `BudgetStatus` shape.

## Capabilities

### New Capabilities

- `poll-status-query`: Ask protocol for querying SchedulerActor poll states from
  outside the actor (message types, response mapping, timeout handling).

### Modified Capabilities

- `server-status-api`: Extend GetStatus response with per-model poll status and
  active enrichment list.
- `poll-scheduler`: Add query handler for GetPollStates message alongside
  existing TriggerImmediatePoll handler.

## Impact

- **Proto**: `config_service.proto` — new messages (`ModelPollStatus`,
  `PollPhase` enum), extended `ServerStatus`.
- **SchedulerActor**: New `Command<GetPollStates>` handler in Ready state +
  message types.
- **ConfigGrpcService**: `GetStatus` becomes async, gains SchedulerActor
  dependency (already has `ActorRegistry`), reads `EnrichmentOptions`.
- **Tests**: New specs for Ask protocol and status mapping.
