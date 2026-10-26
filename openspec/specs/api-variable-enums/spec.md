# api-variable-enums Specification

## Purpose
Open-Meteo API variable names live in per-domain enums in the adapter layer, keeping the domain parameter enums API-free.
## Requirements
### Requirement: Per-domain API variable enums in the adapter layer
Each domain that fetches Open-Meteo data SHALL have a dedicated API variable enum in
`Njord.OpenMeteo` with one member per API variable name. Each member SHALL carry a
`[JsonStringEnumMemberName]` attribute whose value is the exact Open-Meteo API parameter
string (e.g. `"temperature_2m"`).

#### Scenario: Weather API variable enum
- **WHEN** the Weather domain requires `temperature_2m` and `wind_speed_10m`
- **THEN** the `WeatherApiVariable` enum SHALL have members attributed with those exact strings

#### Scenario: All domains covered
- **WHEN** a domain has at least one API variable
- **THEN** an API variable enum SHALL exist for that domain

### Requirement: Domain-to-API enum mapping via explicit switch
The mapping from domain flags enum members (e.g. `WeatherParam.Temperature`) to API variable
enum members (e.g. `WeatherApiVariable.Temperature`) SHALL be an explicit switch expression
with one arm per member and a default that throws `ArgumentOutOfRangeException`.

#### Scenario: Every domain param member maps
- **WHEN** a domain param flags enum has N members
- **THEN** the mapping switch SHALL have exactly N arms

#### Scenario: Unknown member throws
- **WHEN** a domain param value does not match any switch arm
- **THEN** the mapping SHALL throw `ArgumentOutOfRangeException`

### Requirement: Cached string lookup from API variable enums
A `FrozenDictionary` per domain SHALL map each API variable enum member to its API string
name, built once from the `[JsonStringEnumMemberName]` attributes. The `ToApiName` extension
method SHALL use this dictionary for O(1) lookup.

#### Scenario: Lookup returns the attributed name
- **WHEN** `WeatherApiVariable.Temperature.ToApiName()` is called
- **THEN** it SHALL return `"temperature_2m"`

#### Scenario: Dictionary is built once
- **WHEN** `ToApiName` is called multiple times
- **THEN** the backing dictionary SHALL be a static field, not rebuilt per call

### Requirement: Domain enums in Njord.Forecast stay API-free
The `XxxParams` flags enums in `Njord.Forecast` SHALL NOT carry any Open-Meteo-specific
attributes or string literals. API naming is the exclusive responsibility of the API variable
enums in `Njord.OpenMeteo`.

#### Scenario: No serialization attributes on domain params
- **WHEN** `WeatherParam` is inspected
- **THEN** it SHALL have no `[JsonStringEnumMemberName]` or `[EnumMember]` attributes

### Requirement: Completeness test per domain
Each domain SHALL have a theory test asserting that every member of the domain flags enum
has a corresponding member in the API variable enum and an entry in the frozen dictionary.

#### Scenario: New domain param without API variable
- **WHEN** a new member is added to `WeatherParam` without a matching `WeatherApiVariable` member
- **THEN** the completeness test SHALL fail

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

