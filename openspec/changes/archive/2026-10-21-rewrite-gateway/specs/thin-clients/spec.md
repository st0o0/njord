## REMOVED Requirements

### Requirement: Variables supplied by the caller
**Reason**: Clients are thick; they resolve their own variables and forecast days.
**Migration**: See "Clients derive request from resolution and options".

### Requirement: Gateway derives request from resolution and options
**Reason**: The gateway no longer knows variables or options.
**Migration**: See "Clients derive request from resolution and options".

## ADDED Requirements

### Requirement: Clients derive request from resolution and options
Each client SHALL take its variables from the resolved feature requirements and its forecast days from its own domain options, clamped to the API maximum.

#### Scenario: Requested variables in URL
- **WHEN** AirQuality is resolved with two variables and 5 forecast days
- **THEN** the request URL SHALL list exactly those two hourly variables and 5 forecast days
