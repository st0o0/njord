## ADDED Requirements

### Requirement: Parameter selection resolves configuration into a parameter set
`ParameterSelection` in `Njord.Core/Configuration` SHALL resolve group names, extra variable names and excluded variable names into a `ResolvedParameterSet` using `enabled = union(groups) ∪ extra \ exclude`, partitioned into hourly and daily subsets. Group names SHALL be parsed case-insensitively by an exhaustive switch over the `ParameterGroup` members, not by `Enum.TryParse`.

#### Scenario: Default configuration resolves the Weather group
- **WHEN** the selection is resolved with `Groups: ["Weather"]` and no extras or excludes
- **THEN** the resolved set contains all Weather-group hourly and daily parameters

#### Scenario: Group names are case-insensitive
- **WHEN** the selection is resolved with `Groups: ["weather", "SOLAR"]`
- **THEN** the Weather and Solar groups are included

#### Scenario: Unknown group
- **WHEN** the selection contains the group `Marine`
- **THEN** resolution fails with an error naming the unknown group and listing the valid groups

### Requirement: Selection errors are collected and reported together
Resolution SHALL collect every error (unknown group, unknown extra, unknown exclude, empty result) and throw one `ParameterResolutionException` that carries all messages. `ParameterResolutionException` SHALL live in `Njord.Core/Configuration`.

#### Scenario: Several errors at once
- **WHEN** the selection has an unknown group and an unknown extra
- **THEN** one exception is thrown whose `Errors` contains both messages

### Requirement: The registry has no configuration logic
`ParameterRegistry` SHALL NOT parse configuration, build operator messages or reference `Enum.TryParse`. The enum-convention architecture spec SHALL no longer allow-list it.

#### Scenario: Allow-list shrinks
- **WHEN** the enum-conversion architecture spec runs
- **THEN** it has no allow-list entry for `ParameterRegistry` and passes
