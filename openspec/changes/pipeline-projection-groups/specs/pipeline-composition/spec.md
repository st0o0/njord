## MODIFIED Requirements

### Requirement: Pipeline input is DomainDataReceived
The pipeline's MergeHub SHALL accept DomainDataReceived(Location, DomainKind, IDomainForecast) messages instead of Forecast messages. Each successful domain fetch from any PollWorker produces one DomainDataReceived.

#### Scenario: Multiple domains arrive independently
- **WHEN** Weather data arrives at t=0 and AirQuality data arrives at t=200ms for the same location
- **THEN** the pipeline SHALL process each as a separate DomainDataReceived, updating the LocationForecast incrementally

### Requirement: No settings or sensor side-channels
The pipeline SHALL have no ZipWith side-channels. Analysis settings are constructor-injected from IOptions<T>. No sensor readings input exists.

#### Scenario: Direct flow from MergeHub to StatefulSelectMany
- **WHEN** the pipeline is materialized
- **THEN** the graph SHALL be MergeHub → StatefulSelectMany → BroadcastHub with no ZipWith stages
