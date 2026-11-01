## MODIFIED Requirements

### Requirement: Default ensemble models
The system SHALL configure default Ensemble Mean models as `["ecmwf_ifs025_ensemble_mean", "ncep_gefs025_ensemble_mean"]`. The GFS model identifier SHALL use the `ncep_gefs025_ensemble_mean` API ID, not `gfs025_ensemble_mean`.

#### Scenario: Default models produce valid API requests
- **WHEN** `EnsembleOptions.Models` is not overridden by the operator
- **THEN** the applied defaults SHALL be `["ecmwf_ifs025_ensemble_mean", "ncep_gefs025_ensemble_mean"]`
- **AND** requests to the Ensemble Mean API SHALL return HTTP 200 for both models

#### Scenario: Operator overrides models
- **WHEN** the operator sets `Njord:Ensemble:Models` to a custom list
- **THEN** the system SHALL use the operator-provided model IDs unchanged
