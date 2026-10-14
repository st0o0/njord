## ADDED Requirements

### Requirement: GetEnrichments returns the enrichment payloads
`WeatherService.GetEnrichments` SHALL accept `GetEnrichmentsRequest` with `string location` and return `GetEnrichmentsResponse` with `string location = 1` and optional payload fields `AlertUpdate alerts = 2`, `IndexUpdate indices = 3`, `TrendUpdate trends = 4`, `DerivedUpdate derived = 5`, `ConsensusUpdate consensus = 7` and `google.protobuf.Timestamp consensus_updated_at = 8`. Field number 6 (the removed history payload) SHALL be `reserved`; there SHALL be no energy-related field.

#### Scenario: All enabled enrichments returned
- **WHEN** a client calls `GetEnrichments` for a location with consensus, alerts, and trends enabled
- **THEN** the response SHALL contain non-null `consensus`, `alerts`, and `trends` payloads

#### Scenario: Removed history field stays reserved
- **WHEN** `GetEnrichmentsResponse` is inspected in the proto
- **THEN** field number 6 is `reserved` and no history payload exists

### Requirement: StreamEnrichments streams analysis events
`WeatherService.StreamEnrichments` SHALL be a server-streaming RPC accepting `StreamEnrichmentsRequest` with optional `string location`. Each `EnrichmentEvent` SHALL contain `string location`, `string type_name`, `google.protobuf.Timestamp updated_at`, and a `oneof payload` with the cases `AlertUpdate alerts = 10`, `IndexUpdate indices = 11`, `TrendUpdate trends = 12`, `DerivedUpdate derived = 13` and `ConsensusUpdate consensus = 15` (number 14, the removed history payload, is unused). It SHALL NOT contain energy-related entries.

#### Scenario: Stream delivers enrichment events
- **WHEN** a new analysis result is computed
- **THEN** all subscribers matching the location filter SHALL receive an `EnrichmentEvent`

#### Scenario: Oneof without history case
- **WHEN** `EnrichmentEvent` is inspected in the proto
- **THEN** the oneof has the five analysis cases and no history or energy case

## REMOVED Requirements

### Requirement: GetEnrichments returns all enrichment payloads
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "GetEnrichments returns the enrichment payloads" in this capability.

### Requirement: StreamEnrichments streams enrichment events
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "StreamEnrichments streams analysis events" in this capability.
