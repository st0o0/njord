## ADDED Requirements

### Requirement: Scalar defaults live on the property
Every scalar options property (int, double, bool, TimeSpan, enum, string with a known default) SHALL have an explicit property initializer matching the shipped default.

#### Scenario: Reading a default from the class
- **WHEN** a developer reads an options class
- **THEN** every scalar property SHALL show its default value as a property initializer

#### Scenario: new XOptions() produces correct defaults
- **WHEN** constructing a scalar-only options class with the default constructor
- **THEN** all properties SHALL have their shipped default values

### Requirement: List defaults stay in code with ReplaceCollectionDefaults
Options properties that are lists or collections SHALL keep their defaults as property initializers and use ReplaceCollectionDefaults for configuration override.

#### Scenario: Config overrides a list
- **WHEN** configuration provides values for a list property
- **THEN** ReplaceCollectionDefaults SHALL replace the entire code default, not merge by index

### Requirement: appsettings.json carries structure, not duplicated defaults
appsettings.json SHALL contain section headers and keys for configuration binding but SHALL NOT duplicate values that match property initializers.

#### Scenario: appsettings.json after migration
- **WHEN** inspecting the Njord section in appsettings.json
- **THEN** scalar keys whose values match property initializers SHALL be absent

### Requirement: AppSettingsDefaults stays for list-default classes
Test helper AppSettingsDefaults SHALL remain for options classes that have list defaults requiring ReplaceCollectionDefaults binding.

#### Scenario: Scalar-only options in tests
- **WHEN** a test needs default values of a scalar-only options class
- **THEN** it MAY use `new T()` directly (arch spec allows it)
