## Why

OpenMeteoGateway is 422 lines with per-domain boilerplate: each domain adds a private FetchDone record, a handler, and a PipeTo chain. Adding Marine would copy the pattern again. The gateway should route generically by DomainKind. OpenMeteoClient duplicates HTTP+parse boilerplate across 3 fetch methods. EnabledDomains is hardcoded in PollWorker.

## What Changes

- Generic domain routing: one `DomainFetchDone(DomainKind, IDomainForecast)` record instead of per-domain records
- OpenMeteoClient: extract shared HTTP+parse into a private helper, each endpoint method calls it
- EnabledDomains from config: `NjordOptions.EnabledDomains` (default: Weather, AirQuality, Flood)
- PollWorker reads enabled domains from options at construction

## Non-goals

- Adding new domains. Just simplifying the pattern for when they're added.

## Capabilities

### New Capabilities
- `gateway-simplification`: Generic domain routing in gateway

### Modified Capabilities
- `open-meteo-multi-endpoint`: Simplified routing
- `multi-domain-polling`: Config-driven domain selection

## Impact

- **Njord.OpenMeteo**: Gateway simplified, client deduplicated
- **Njord.Pipeline**: PollWorker reads domains from config
- **Njord.Core**: NjordOptions gains EnabledDomains
