## REMOVED Requirements

### Requirement: AlertComputed pipeline event
**Reason**: Replaced by AlertRaised in the projection-group pipeline.
**Migration**: All consumers already handle AlertRaised.

#### Scenario: No references to AlertComputed remain
- **WHEN** the codebase is searched for AlertComputed
- **THEN** zero production or test references SHALL exist

### Requirement: IWithTargetId interface
**Reason**: Replaced by IWithLocationId.
**Migration**: All messages implement IWithLocationId.

#### Scenario: No references to IWithTargetId remain
- **WHEN** the codebase is searched for IWithTargetId
- **THEN** zero references SHALL exist

## MODIFIED Requirements

### Requirement: CatalogLocation uses Location directly
CatalogLocation SHALL carry a Location (which includes coordinates) instead of separate Latitude and Longitude fields.

#### Scenario: Coordinates come from Location
- **WHEN** a CatalogLocation is constructed
- **THEN** its Location.Latitude and Location.Longitude SHALL match the configured coordinates
