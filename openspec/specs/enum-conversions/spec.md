# enum-conversions Specification

## Purpose
TBD - created by archiving change typed-mqtt-payloads. Update Purpose after archive.
## Requirements
### Requirement: The convention is enforced
An architecture spec SHALL fail when production code (outside an allow-list for user-written configuration parsing) calls `Enum.Parse`, `Enum.TryParse` or builds text with `ToString().ToLower`/`ToUpper` on an enum.

#### Scenario: New name-based conversion
- **WHEN** a production file adds `kind.ToString().ToLowerInvariant()` for a wire value
- **THEN** the architecture spec fails

