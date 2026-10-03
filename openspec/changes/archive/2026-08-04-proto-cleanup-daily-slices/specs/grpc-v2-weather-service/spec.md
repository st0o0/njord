## MODIFIED Requirements

### Requirement: GetEnrichmentsResponse contains enrichment payloads

`GetEnrichmentsResponse` SHALL contain fields: `string location`, `AlertUpdate alerts`, `IndexUpdate indices`, `TrendUpdate trends`, `DerivedUpdate derived`, `HistoryUpdate history`, `ConsensusUpdate consensus`, `google.protobuf.Timestamp consensus_updated_at`. It SHALL NOT contain any `reserved` statements or energy-related fields.

#### Scenario: Response without reserved gaps

- **WHEN** `GetEnrichmentsResponse` is inspected in the proto
- **THEN** field numbers SHALL be sequential with no gaps and no `reserved` statements

### Requirement: EnrichmentEvent oneof payload

`EnrichmentEvent` SHALL contain `string location`, `string type_name`, `google.protobuf.Timestamp updated_at`, and a `oneof payload` with cases: `AlertUpdate alerts`, `IndexUpdate indices`, `TrendUpdate trends`, `DerivedUpdate derived`, `HistoryUpdate history`, `ConsensusUpdate consensus`. It SHALL NOT contain any `reserved` fields, comments about removed features, or energy-related entries.

#### Scenario: Oneof without energy gap

- **WHEN** `EnrichmentEvent` is inspected in the proto
- **THEN** the oneof field numbers SHALL be sequential with no gaps or comments about removed fields
