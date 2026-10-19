## Context

PollWorker is sharded by `"{location}|{model}"` with one worker per combination. The redesign makes it one worker per location, fetching multiple domain endpoints.

## Goals / Non-Goals

**Goals:** PollWorker per location, multi-domain parallel fetching, Ensemble endpoint.
**Non-Goals:** Changing pipeline processing (separate change).

## Decisions

### D1: Entity-id becomes location name only

ShardMessageExtractor uses `Location.Name` as entity-id. Messages implement `IWithLocationId` (replaces `IWithTargetId`). Persistence id: `"poll-worker-{location}"`.

### D2: PollWorker fetches domains in parallel

On tick: for each enabled domain, check budget, send FetchDomain to Gateway. Collect results with timeout. Emit `DomainDataReceived` for each successful fetch to the pipeline's SinkRef. Failed domains are retried next cycle.

### D3: Gateway routes by DomainKind

`FetchDomain(Location, DomainKind)` replaces `FetchForecast`. Gateway selects the correct endpoint URL and parser based on DomainKind. Throttle is shared across all domain calls (same Open-Meteo rate limit).

### D4: Ensemble replaces per-model calls

Default: one Ensemble Mean call per location (mean + std dev for all parameters). Optional: individual model calls alongside ensemble (configurable, for model comparison).

### D5: PollWorkerState simplified

No per-model tracking. State tracks: phase per domain, last model-run per domain, failure counts. Persistence events change (breaking — 0.x allowed).

## Risks / Trade-offs

- **[Breaking persistence]** → 0.x allows it. Old journals ignored on upgrade.
- **[Ensemble Mean API is newer]** → Verified available since March 2026.
