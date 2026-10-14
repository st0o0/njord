## MODIFIED Requirements

### Requirement: StreamForecasts streams per-model updates
`WeatherService.StreamForecasts` SHALL be a server-streaming RPC accepting `StreamForecastsRequest` with optional `string location` (empty = all). A non-empty `location` SHALL match a configured location case-insensitively; an unconfigured location SHALL fail the call with gRPC `NOT_FOUND`. Each `ForecastUpdate` SHALL contain `string location`, `string model`, `google.protobuf.Timestamp updated_at`, `repeated HourlyForecast hourly`, `repeated DailyForecast daily`.

#### Scenario: Stream filters by location
- **WHEN** a client subscribes with `location = "Lucerne"`
- **THEN** only forecast updates for "Lucerne" SHALL be streamed

#### Scenario: Empty location streams all updates
- **WHEN** a client subscribes with empty `location`
- **THEN** forecast updates for all locations SHALL be streamed

#### Scenario: Unknown location returns NOT_FOUND
- **WHEN** a client subscribes with a `location` that is not configured
- **THEN** the RPC SHALL throw a gRPC NOT_FOUND error instead of opening a stream that never delivers

#### Scenario: Multiple clients receive the same update
- **WHEN** two clients have active `StreamForecasts` streams and a poll cycle completes
- **THEN** both clients SHALL receive the `ForecastUpdate`

#### Scenario: Stream ends on client disconnect
- **WHEN** a client disconnects from the `StreamForecasts` stream
- **THEN** the server-side stream SHALL be shut down and its egress subscription released

### Requirement: StreamEnrichments streams analysis events
`WeatherService.StreamEnrichments` SHALL be a server-streaming RPC accepting `StreamEnrichmentsRequest` with optional `string location` (empty = all). A non-empty `location` SHALL match a configured location case-insensitively and restrict the stream to that location's events; an unconfigured location SHALL fail the call with gRPC `NOT_FOUND`. Each `EnrichmentEvent` SHALL contain `string location`, `string type_name`, `google.protobuf.Timestamp updated_at`, and a `oneof payload` with the cases `AlertUpdate alerts = 10`, `IndexUpdate indices = 11`, `TrendUpdate trends = 12`, `DerivedUpdate derived = 13` and `ConsensusUpdate consensus = 15` (number 14, the removed history payload, is unused). It SHALL NOT contain energy-related entries.

#### Scenario: Stream delivers enrichment events
- **WHEN** a new analysis result is computed
- **THEN** all subscribers matching the location filter SHALL receive an `EnrichmentEvent`

#### Scenario: Enrichment stream filters by location
- **WHEN** a client subscribes with `location = "Lucerne"` and results are computed for "lucerne" and "zurich"
- **THEN** only the `EnrichmentEvent`s of "lucerne" SHALL be streamed

#### Scenario: Empty location streams all enrichment events
- **WHEN** a client subscribes with empty `location`
- **THEN** enrichment events for all locations SHALL be streamed

#### Scenario: Unknown enrichment location returns NOT_FOUND
- **WHEN** a client subscribes with a `location` that is not configured
- **THEN** the RPC SHALL throw a gRPC NOT_FOUND error

#### Scenario: Oneof without history case
- **WHEN** `EnrichmentEvent` is inspected in the proto
- **THEN** the oneof has the five analysis cases and no history or energy case
