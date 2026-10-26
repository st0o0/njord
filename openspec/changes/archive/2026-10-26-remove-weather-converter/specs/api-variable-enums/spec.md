## ADDED Requirements

### Requirement: ApiVariableNames as single source of truth for variable-name splitting
The mapping layer SHALL derive the set of known API variable names exclusively from
`ApiVariableNames.For<WeatherApiVariable>()`. No separate frozen set, constant array, or
hard-coded list of variable name strings SHALL exist for the purpose of key splitting or
variable-name matching.

#### Scenario: Variable names come from the enum attributes
- **WHEN** the weather response mapping splits hourly keys
- **THEN** it SHALL obtain the known names from `ApiVariableNames.For<WeatherApiVariable>().Values`

#### Scenario: Adding a new weather variable
- **WHEN** a new member is added to `WeatherApiVariable` with a `[JsonStringEnumMemberName]`
- **THEN** the key splitter SHALL automatically recognize it without any other code change

#### Scenario: No duplicate name lists
- **WHEN** the codebase is searched for frozen sets or arrays of weather API variable name strings
- **THEN** only `ApiVariableNames`'s internal cache SHALL exist
