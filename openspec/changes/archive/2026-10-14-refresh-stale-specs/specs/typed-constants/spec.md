## MODIFIED Requirements

### Requirement: EnrichmentKind enum replaces bare type-name strings
An `EnrichmentKind` enum (`Consensus`, `Alerts`, `Derived`, `Trends`, `Indices`)
SHALL exist in `Njord.Messages` (namespace `Njord.Messages.Pipeline`). Enrichment `PipelineEvent` variants SHALL implement
`IEnrichmentEvent` exposing `EnrichmentKind Kind`. Wire names SHALL come from `EnrichmentKind.ToWireName()`; no production code SHALL use
bare string literals for enrichment type names.

#### Scenario: PipelineEvent enrichment variants expose Kind
- **GIVEN** a `PipelineEvent.ConsensusComputed` event
- **WHEN** cast to `IEnrichmentEvent`
- **THEN** `Kind` returns `EnrichmentKind.Consensus`

#### Scenario: MQTT topic uses EnrichmentKind instead of string
- **GIVEN** `StatePayloadBuilder` building a topic for an alert result
- **WHEN** it constructs the topic path
- **THEN** it uses `EnrichmentKind.Alerts.ToWireName()` (not the literal `"alerts"`)

#### Scenario: gRPC mapper uses EnrichmentKind instead of string
- **GIVEN** `AnalysisProtoMappings` mapping a `TrendComputed` event
- **WHEN** it assigns the proto `type_name` field
- **THEN** it uses the event's `Kind.ToWireName()` (not a string literal)
