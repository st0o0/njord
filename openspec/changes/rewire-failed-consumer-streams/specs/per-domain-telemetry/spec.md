## MODIFIED Requirements

### Requirement: Meters are registered explicitly in the host
The host SHALL list every exported meter by name in `CoreSetupContainer.NjordMeters` (`Njord.Core`, `Njord.OpenMeteo`, `Njord.Budget`, `Njord.Pipeline`, `Njord.Mqtt`), and the prometheus-net meter adapter SHALL export only instruments whose meter name is in that list. `Njord.Core` owns only the instruments of its shared base classes (the stream consumer rewire counter), defined in its own `internal static class Telemetry`; domain metrics stay in their feature library.

#### Scenario: New domain meter is discoverable
- **WHEN** a new feature library adds metrics
- **THEN** it must add its meter name to `NjordMeters` — a missing entry means its metrics are not exported

#### Scenario: Core meter is exported
- **WHEN** the host configures the meter adapter
- **THEN** `Njord.Core` is in `NjordMeters`, so `njord_stream_consumer_rewires_total` is exported
