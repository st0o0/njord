## MODIFIED Requirements

### Requirement: SchedulerActor iterates resolved models per location
The `SchedulerActor` SHALL resolve effective models per location using
`LocationOptions.ResolveModels(globalModels)` and iterate over the
resolved list. It SHALL NOT iterate the global `Models` list directly.

#### Scenario: Location with extra models gets polled for all
- **WHEN** global Models is `["icon_global"]` and location "berlin" has
  Models `["icon_d2"]`
- **THEN** the scheduler SHALL create poll states for both
  `("berlin", "icon_global")` and `("berlin", "icon_d2")`

#### Scenario: Location without extra models gets global only
- **WHEN** global Models is `["icon_global"]` and location "amsterdam"
  has no Models
- **THEN** the scheduler SHALL create a poll state only for
  `("amsterdam", "icon_global")`
