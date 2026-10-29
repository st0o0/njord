## MODIFIED Requirements

### Requirement: No Effective* workaround properties
Options classes SHALL NOT use nullable properties with computed Effective* fallback properties. Default values SHALL be applied via PostConfigure<T> registrations so that IOptions<T>.Value always carries the resolved value.

#### Scenario: ForecastOptions exposes Models directly
- **WHEN** IOptions<ForecastOptions>.Value is accessed
- **THEN** Models is a non-nullable string[] (never null at runtime)
- **THEN** EffectiveModels and PrimaryModel properties do not exist
- **THEN** the first element of Models is the primary model

#### Scenario: PollingOptions exposes Budget directly
- **WHEN** IOptions<PollingOptions>.Value is accessed after startup
- **THEN** the budget override or free-tier default is directly available without an EffectiveBudget property

## REMOVED Requirements

### Requirement: ForecastOptions.Model migration shim
**Reason**: Dead 0.4.x to 0.5.x migration property. The migration path is in the changelog.
**Migration**: Remove `Model` property and its validator branch.

#### Scenario: ForecastOptions.Model does not exist
- **WHEN** ForecastOptions is inspected
- **THEN** there is no `Model` (singular) property

### Requirement: Duplicate SolarYieldOptions.PeakKw
**Reason**: PeakKw and SystemEfficiency exist in both SolarYieldOptions and SolarYieldConfig.
**Migration**: SolarYieldConfig reads PeakKw and SystemEfficiency from IOptions<SolarYieldOptions>.

#### Scenario: Single source of truth for PeakKw
- **WHEN** the SolarYield feature computes yield
- **THEN** it reads PeakKw and SystemEfficiency from SolarYieldOptions only (section Njord:SolarYield)
- **THEN** SolarYieldConfig does not have PeakKw or SystemEfficiency properties
