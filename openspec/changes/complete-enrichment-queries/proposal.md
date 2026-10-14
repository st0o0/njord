## Why

`GetEnrichments` is meant to return every enrichment of a location in one response, and
`GetEnrichmentsResponse` has a field for each kind (consensus with `consensus_updated_at`, alerts,
indices, trends, derived). `ForecastReadModel` however keeps only the last analysis event per location,
so a response carries at most one kind (in practice whichever analyzer finished last in the cycle).
`StreamEnrichments` also ignores `StreamEnrichmentsRequest.location` and streams every location to every
client, although the specs promise that subscribers only receive events matching their location filter.

## What Changes

- `ForecastReadModel` keeps the latest event per analysis kind per location instead of the single last
  analysis event.
- `LatestAnalysisResult` (`Njord.Messages`) carries one typed, nullable property per analysis kind
  (`Consensus`, `Alerts`, `Derived`, `Trends`, `Indices`) instead of one `PipelineEvent`.
  `LatestAnalysisNotAvailable` is still the answer while no analysis of the location exists.
- `IEnrichmentEvent` exposes the `LocationId` of the event, so consumers filter and key enrichment
  events without a per-variant switch.
- `LatestAnalysisResponse.ToProto()` fills every available payload field of `GetEnrichmentsResponse`;
  `consensus_updated_at` keeps coming from the consensus event's timestamp.
- `GetEnrichments` queries the read model with the location id of the catalog entry it resolved, so a
  location requested in a different letter case finds its data.
- `StreamEnrichments` honours `location`: empty streams every location, a configured location
  (case-insensitive) streams only that location, an unknown location fails with `NOT_FOUND` like
  `GetEnrichments`.
- `StreamForecasts` already filters by location; it now also rejects an unknown non-empty location with
  `NOT_FOUND` (before, such a stream stayed open and silent forever).

## Non-goals

- No change to the proto contract (`weather.proto`): fields, numbers and messages stay as they are.
- No change to the streamed `EnrichmentEvent`/`ForecastUpdate` payloads (the golden masters stay
  unchanged).
- No history of past analysis events in the read model; only the latest per kind.
- No change to how `GetForecast`/`QueryForecast` key their location, and no persistence of the read model.
- No change to analysis computation, polling or MQTT output.

## API budget

No polling change: the poll interval, the number of fetch targets and the request weight are untouched,
so the Open-Meteo usage (against the 300k/month free-tier soft limit) stays exactly as it is.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `consumer-model`: `ForecastReadModel` holds the latest analysis event per kind per location.
- `grpc-enrichment-api`: `GetEnrichments` returns every available kind; `StreamEnrichments` filters by
  location.
- `grpc-v2-weather-service`: `StreamForecasts` and `StreamEnrichments` define empty, matching and unknown
  location semantics.

## Impact

- `src/Njord.Messages/Pipeline/QueryLatestAnalysis.cs`, `PipelineEvent.cs` (contract shape change of
  `LatestAnalysisResult`, new `IEnrichmentEvent.Location`).
- `src/Njord.Pipeline/ForecastReadModel.cs`.
- `src/Njord.Grpc/WeatherGrpcService.cs`, `src/Njord.Grpc/Mapping/AnalysisProtoMappings.cs`.
- Specs in `Njord.Pipeline.Tests` and `Njord.Grpc.Tests`.
- gRPC-visible for ha-njord: `GetEnrichments` now returns all enrichment kinds at once;
  `StreamEnrichments` with a location now only delivers that location; a stream call with an unknown
  location ends with `NOT_FOUND` instead of staying open.
