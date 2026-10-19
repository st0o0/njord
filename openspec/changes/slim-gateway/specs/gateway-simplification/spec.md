## MODIFIED Requirements

### Requirement: Gateway routes domains generically
The OpenMeteoGateway SHALL handle all domains through a single generic routing path. Adding a new domain SHALL NOT require a new private record type or handler method.

#### Scenario: All domains use one completion record
- **WHEN** a Weather fetch and a Flood fetch both complete
- **THEN** both SHALL be processed through the same DomainFetchDone handler

### Requirement: Enabled domains are configurable
PollWorker SHALL read enabled domains from NjordOptions. The default SHALL be Weather, AirQuality, Flood. Disabling a domain SHALL prevent its fetch without code changes.

#### Scenario: Disable AirQuality via config
- **WHEN** `Njord:EnabledDomains` is set to `["Weather", "Flood"]`
- **THEN** PollWorker SHALL NOT fetch AirQuality data
