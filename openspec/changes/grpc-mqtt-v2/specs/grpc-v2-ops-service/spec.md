## MODIFIED Requirements

### Requirement: GetStatus reflects multi-domain polling
OpsService.GetStatus SHALL report poll status per location (not per location×model), with per-domain fetch status within each location.

#### Scenario: Status shows domain-level detail
- **WHEN** a client calls GetStatus
- **THEN** each location SHALL show fetch status for each enabled domain (last fetch time, success/failure, model run time)
