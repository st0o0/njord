## Why

`OpenMeteoGateway` is a 290-line singleton with 17 constructor dependencies. Every new domain (Seasonal and Satellite were the latest) adds a client, an options object, a variable list and a `switch` arm. The six clients are thin HTTP wrappers; the gateway does the mapping, trimming, error classification and fetch telemetry for all of them. Each client also builds its URL through `OpenMeteoRequestBuilder`, a hand-rolled string builder. The gateway is hard to read, hard to test per domain, and every domain change touches it.

The reference style (FunkArr: `TvdbClient`, `TmdbClient`, `MediathekClient`) is the opposite: thick clients that return domain results, a thin orchestrator.

## What Changes

- **BREAKING (internal)**: add `IDomainClient` (`DomainKind Domain`, `Task<FetchResult> FetchAsync(double latitude, double longitude, DateTimeOffset now, CancellationToken)`) and the `FetchResult` union (`FetchSuccess`, `FetchEmpty`, `FetchError`) in `Njord.Core`.
- Each of the six clients (`WeatherClient`, `AirQualityClient`, `FloodClient`, `MarineClient`, `SeasonalClient`, `SatelliteRadiationClient`) implements `IDomainClient` and becomes thick: it builds its URL, calls the API, maps the response to its `DomainForecast<T>`, trims trailing empty points, turns every HTTP/JSON/timeout failure into a `FetchError`, and records fetch telemetry. Per-domain options and the resolved variable list are injected into the client.
- The per-domain `I*Client` interfaces and the old `FetchAsync(..., variables, days, ...)` signatures are removed.
- `OpenMeteoGateway` becomes thin: it takes `IReadOnlyDictionary<DomainKind, IDomainClient>`, `IGatewayBudgetGate`, `FetchTimeouts` (plus what location lookup and time need, see design). It orchestrates budget check, client dispatch, pipeline offer and reply, and keeps the pipeline attachment. Target 100 lines or fewer.
- `OpenMeteoRequestBuilder` is deleted; clients build URLs with `QueryHelpers.AddQueryString`.
- Budget weight moves to the client side of the contract so the gateway no longer knows about Weather.

## Capabilities

### New Capabilities
- None. Behavior is preserved; the gateway keeps its request/response contract (exactly one `FetchDomainCompleted`, `FetchDomainFailed` or `FetchDomainRejected`). No spec is written because nothing observable changes; client behavior is pinned by client specs.

### Modified Capabilities
- None.

## Non-goals

- No change to polling, budget formulas, persistence records, pipeline, gRPC or MQTT.
- No new domains or variables.
- No retry or resilience changes (the typed `HttpClient` pipelines stay).

## API-budget impact

None. Same requests, same parameter set and values, same call weights. Verified by URL specs per client.

## Impact

- `src/Njord.Core`: new `Domain/IDomainClient.cs`, `Domain/FetchResult.cs`.
- `src/Njord.OpenMeteo`: all six clients, `OpenMeteoGateway.cs`, the OpenMeteo setup container registration, deleted `OpenMeteoRequestBuilder.cs` and `I*Client.cs`; `Telemetry.cs` is used from the clients. Mappings stay.
- `src/Njord.OpenMeteo.Tests`, `src/Njord.Tests.Shared` (fake clients), `src/Njord.IntegrationTests` (client fakes in host setup).
- Prerequisite of `multi-model-weather`.
