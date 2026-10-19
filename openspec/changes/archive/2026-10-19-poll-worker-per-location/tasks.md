## 1. OpenMeteo Ensemble endpoint

- [x] 1.1 Add Ensemble Mean endpoint URL building to `src/Njord.OpenMeteo/`
- [x] 1.2 Add Ensemble response DTOs and JSON parsing
- [x] 1.3 Add mapping from Ensemble response to `DomainForecast<WeatherPoint>`
- [x] 1.4 Tests for Ensemble endpoint (URL, parsing, mapping)

## 2. Gateway multi-domain routing

- [x] 2.1 Extend `FetchForecast` to `FetchDomain(Location, DomainKind)` or add new message
- [x] 2.2 Gateway routes to correct endpoint based on DomainKind
- [x] 2.3 Response mapped to `DomainDataReceived(Location, DomainKind, IDomainForecast)`
- [x] 2.4 Tests for gateway routing

## 3. PollWorker per-location redesign

- [x] 3.1 Change entity-id from `"{location}|{model}"` to `"{location}"`
- [x] 3.2 Update `IWithTargetId` to `IWithLocationId` in messages
- [x] 3.3 Update `ShardMessageExtractor` for new entity-id format
- [x] 3.4 PollWorker fetches N domains in parallel on tick
- [x] 3.5 Collect results with timeout, emit `DomainDataReceived`
- [x] 3.6 Update PollWorkerState for per-domain tracking
- [x] 3.7 Update persistence events (breaking 0.x change)
- [x] 3.8 Tests for multi-domain polling flow

## 4. PollManager simplified

- [x] 4.1 Target set derived from locations only (not locations × models)
- [x] 4.2 StartPolling/StopPolling based on location changes
- [x] 4.3 Tests for PollManager with location-only targets

## 5. Budget accounting

- [x] 5.1 Budget tracks per-domain calls (each domain call = 1 budget unit)
- [x] 5.2 Startup projection updated for multi-domain call count
- [x] 5.3 Tests for budget with domain-based accounting

## 6. Validation

- [x] 6.1 `dotnet build Njord.slnx`
- [x] 6.2 Full test suite passes
