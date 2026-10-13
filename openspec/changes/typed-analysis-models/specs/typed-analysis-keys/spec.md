## Purpose

Makes horizons, index kinds, locations and categorical analysis results typed values inside
njord, so wrong keys and wrong category text cannot compile, while the text forms consumers
see stay exactly as they are.

## ADDED Requirements

### Requirement: Horizons are typed values
Analysis data keyed by forecast horizon SHALL be keyed by a typed horizon value that is
either an hourly offset or a daily offset. The text forms `h<hours>` and `d<days>` SHALL be
produced only when writing MQTT topics, JSON keys or proto fields, through one shared
formatting function.

#### Scenario: Hourly horizon text form
- **WHEN** the hourly horizon +12 h is written to an output
- **THEN** its text form is `h12`

#### Scenario: Daily horizon text form
- **WHEN** the daily horizon at day offset 1 is written to an output
- **THEN** its text form is `d1`

#### Scenario: Hourly and daily horizons are distinct
- **WHEN** the hourly horizon +1 h and the daily horizon day 1 are compared
- **THEN** they are not equal

#### Scenario: No horizon strings in compute code
- **WHEN** the analysis computation code is inspected
- **THEN** it does not build or parse `h<n>`/`d<n>` strings

### Requirement: Index kinds are typed
Activity and utility indices SHALL be identified by a closed set of index kinds: Laundry,
Outdoor, Running, Cycling, Bbq, Irrigation, Solar, NightVentilation. Configuration keys
for per-score overrides SHALL remain the case-insensitive score names; an unknown score name
in configuration SHALL fail startup validation.

#### Scenario: Config key maps to index kind
- **WHEN** configuration contains a score override keyed `running`
- **THEN** it applies to the Running index kind

#### Scenario: Unknown score name rejected
- **WHEN** configuration contains a score override keyed `Swimming`
- **THEN** startup validation fails naming the unknown score

### Requirement: Locations are typed in analysis results
Analysis results SHALL identify their location by a typed location identity whose text
form is the configured location name. Comparing two location identities SHALL be
case-insensitive, matching how configured location names are compared today.

#### Scenario: Case-insensitive identity
- **WHEN** location identities are created from `Lucerne` and `lucerne`
- **THEN** they are equal

### Requirement: Categorical results are typed
Trend direction (rising, falling, stable), stability (converging, diverging, stable),
weather category (clear, fog, drizzle, rain, snow, showers, thunderstorm), dew-point comfort
(dry, comfortable, sticky, oppressive, dangerous) and WMO weather condition SHALL be
closed typed values. Their lowercase wire text and English WMO descriptions SHALL be
produced at the output edge and SHALL equal today's text.

#### Scenario: Wire text unchanged
- **WHEN** a rising trend is published via MQTT
- **THEN** the payload contains `rising`, exactly as before this change
