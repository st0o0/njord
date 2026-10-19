## ADDED Requirements

### Requirement: Remove compiler-guaranteed record tests
Tests that verify compiler-generated behavior (record property accessors, positional record equality, constructor field assignment) SHALL be removed when no custom logic is involved.

#### Scenario: Record with no custom equality or validation
- **WHEN** a test asserts that a positional record's property returns the value passed to its constructor
- **THEN** the test SHALL be removed (the compiler guarantees this)

#### Scenario: Record with custom equality
- **WHEN** a record has custom `IEquatable<T>`, case-insensitive comparison, or overridden `GetHashCode`
- **THEN** tests for that equality behavior SHALL be kept

### Requirement: Eliminate redundant per-member wire-name tests
Per-member `[InlineData]` rows for enum-to-wire-name conversions SHALL be removed when a completeness test (`cover_every_*_member` or `Every_*_member_has_a_*_mapping`) already exists in the same spec.

#### Scenario: Wire-name spec with completeness test
- **WHEN** a spec has both per-member `[InlineData(EnumMember, "wire_name")]` Theory rows AND a completeness test that iterates all enum members
- **THEN** the per-member Theory and its data rows SHALL be removed; the completeness test SHALL be kept

#### Scenario: Wire-name spec without completeness test
- **WHEN** a spec has per-member rows but no completeness test
- **THEN** the per-member rows SHALL be kept (they are the only coverage)

### Requirement: Remove third-party library behavior tests
Tests that exercise only third-party library behavior (not project code) SHALL be removed.

#### Scenario: Test wrapping a library call with no project logic
- **WHEN** a test only verifies that a Google.Protobuf, System.Text.Json, or similar library method returns expected output without any project-specific wrapping
- **THEN** the test SHALL be removed

### Requirement: Remove basic collection behavior tests
Tests that verify standard `ImmutableDictionary` or `Dictionary` operations (add, overwrite, empty check) on a state record SHALL be removed when no custom logic is involved.

#### Scenario: State with only dictionary delegation
- **WHEN** a state method delegates directly to an immutable collection method without additional logic
- **THEN** tests for that delegation SHALL be removed

#### Scenario: State with filtering, aggregation, or case logic
- **WHEN** a state method applies filtering (expiry), aggregation (multi-location), or case-insensitive lookup
- **THEN** tests for that behavior SHALL be kept

### Requirement: Consolidate TelemetrySpec files to golden masters
Per-instrument name/unit assertions in `TelemetrySpec` files SHALL be replaced with a single Verify golden master per project.

#### Scenario: Project with TelemetrySpec asserting individual instrument names
- **WHEN** a project has a `TelemetrySpec` that asserts `instrument.Name == "njord_..."` for each instrument
- **THEN** the individual assertions SHALL be replaced by a single Verify snapshot test that captures all instrument metadata

#### Scenario: Verify snapshot catches instrument rename
- **WHEN** a developer renames a metric instrument
- **THEN** the Verify snapshot SHALL fail, requiring explicit approval of the new name

### Requirement: Update CLAUDE.md test counts
After all test removals, the test count in CLAUDE.md SHALL be updated to match the actual count.

#### Scenario: Test count after cleanup
- **WHEN** all low-value tests have been removed and golden masters added
- **THEN** the total and per-project counts in CLAUDE.md SHALL reflect the new reality
