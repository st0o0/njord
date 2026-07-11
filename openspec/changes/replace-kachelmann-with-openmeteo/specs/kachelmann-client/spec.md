# kachelmann-client Specification (Delta)

## REMOVED Requirements

### Requirement: Advanced forecast fetch per location and model
**Reason**: The Kachelmann API is replaced by Open-Meteo; the fetch behavior
is re-specified for the new provider.
**Migration**: Covered by `openmeteo-client` — Requirement "Forecast fetch per
location and model".

### Requirement: Expected failures are typed outcomes, not exceptions
**Reason**: Provider replaced; the taxonomy changes (`AuthFailed` disappears —
Open-Meteo has no authentication).
**Migration**: Covered by `openmeteo-client` — Requirement "Expected failures
are typed outcomes, not exceptions" (reasons: `RateLimited`,
`ModelUnavailable`, `MalformedPayload`, `Transport`).

### Requirement: No automatic retries
**Reason**: Provider replaced; the requirement itself survives unchanged under
the new capability.
**Migration**: Covered by `openmeteo-client` — Requirement "No automatic
retries".

### Requirement: The API key never leaks
**Reason**: Open-Meteo requires no API key; there is no secret left to
protect in the ingest zone.
**Migration**: None — delete key handling. The `Njord__ApiKey` environment
variable ceases to exist (see `service-configuration` delta).
