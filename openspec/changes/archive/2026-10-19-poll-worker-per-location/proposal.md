## Why

Today there is one sharded PollWorker per (Location, Model) — 5-8 workers per location, each making one API call. In the new architecture, one PollWorker per Location fetches all enabled domains (Weather Ensemble, AirQuality, Flood, Solar, Marine) in parallel. This dramatically reduces API calls (1 Ensemble call vs 5-8 model calls) and enables the multi-domain LocationForecast.

## What Changes

- **BREAKING**: PollWorker entity-id changes from `"{location}|{model}"` to `"{location}"`.
- PollWorker fetches N domain endpoints in parallel per tick, budget-checked per domain call.
- OpenMeteoClient extended with Ensemble endpoint + AirQuality + Flood + Solar + Marine endpoints.
- OpenMeteoGateway handles multi-domain fetches (DomainKind-aware routing).
- PollManager derives target set from locations only (not locations × models).
- PollWorker persistence events change (different entity shape).
- Budget accounting per domain-call instead of per-model-call.

## Non-goals

- Changing the pipeline processing — that's `pipeline-projection-groups`.
- Adding actual enrichments for new domains — that's Phase 4.
- API budget impact: **positive** — 4 calls/cycle (Ensemble + AQ + Flood + Solar) vs 5-8 (individual models), with more data per call.

## Capabilities

### New Capabilities

- `multi-domain-polling`: PollWorker per location fetching multiple Open-Meteo endpoints.
- `ensemble-forecast`: OpenMeteoClient Ensemble Mean endpoint (mean + std dev).
- `open-meteo-multi-endpoint`: Gateway routing to forecast, ensemble, air-quality, flood, solar, marine endpoints.

### Modified Capabilities

- `poll-scheduler`: PollWorker entity changes from per-model to per-location.
- `poll-pipeline`: PollManager target derivation simplified.
- `openmeteo-client`: Extended with new endpoints.

## Impact

- **Njord.Pipeline**: PollWorker, PollManager, PollWorkerState redesigned.
- **Njord.OpenMeteo**: Client extended for all endpoints, new DTOs and JSON parsing.
- **Njord.Persistence**: PollWorker persistence events change (breaking — 0.x).
- **Njord.Budget**: Budget accounting changes.
- **Njord.Core**: ShardMessageExtractor updated for new entity-id format.
