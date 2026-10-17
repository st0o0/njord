# enum-conversions Specification

## Purpose
TBD - created by archiving change typed-mqtt-payloads. Update Purpose after archive.
## Requirements
### Requirement: Enum conversions are explicit switch expressions
Every conversion from an enum to another enum or to text that leaves the process (wire value, MQTT topic segment, metric label) SHALL be an extension method implemented as a `switch` expression with one arm per member and a default that throws `ArgumentOutOfRangeException`. Conversions SHALL NOT use casts between enum types, `Enum.Parse`/`Enum.TryParse`, or `ToString()`-derived text.

#### Scenario: Topic segment for every alert type
- **WHEN** `ToTopicSegment()` is called for each `AlertType` member
- **THEN** it returns the documented segment (for example `heavy-rain`) and no member relies on a fallback

#### Scenario: Enrichment kind wire name
- **WHEN** `ToWireName()` is called for each `EnrichmentKind` member
- **THEN** it returns `consensus`, `alerts`, `derived`, `trends` or `indices` from an explicit arm

#### Scenario: Undefined value
- **WHEN** a conversion receives an undefined enum value
- **THEN** it throws `ArgumentOutOfRangeException`

### Requirement: The convention is enforced
An architecture spec SHALL fail when production code (outside an allow-list for user-written configuration parsing) calls `Enum.Parse`, `Enum.TryParse` or builds text with `ToString().ToLower`/`ToUpper` on an enum.

#### Scenario: New name-based conversion
- **WHEN** a production file adds `kind.ToString().ToLowerInvariant()` for a wire value
- **THEN** the architecture spec fails

