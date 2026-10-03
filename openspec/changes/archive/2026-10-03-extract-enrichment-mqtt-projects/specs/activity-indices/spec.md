## MODIFIED Requirements

### Requirement: Discovery excludes HDD and CDD components
The indices presenter's `BuildDiscoveryPayload` SHALL NOT register sensor components for `hdd` or `cdd`.

#### Scenario: Discovery without degree day sensors
- **WHEN** discovery payload is built for indices
- **THEN** components do not include `hdd` or `cdd`
