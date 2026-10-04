## Purpose

Defines the multi-day forecast attribute on activity index sensors and the supporting data models (DayScoreData, FrostData, VpdData) that represent daily score slices and sub-model data from njord's IndexUpdate.

## Requirements

### Requirement: Activity index sensors expose multi-day forecast
Each activity index sensor SHALL include a `forecast` extra state attribute containing a list of upcoming days' scores. The list SHALL contain one entry per forecast day beyond today, ordered by `day_offset`.

#### Scenario: Sensor shows today's score as state with forecast attribute
- **WHEN** enrichment data contains index days `[{day_offset: 0, laundry: 85}, {day_offset: 1, laundry: 72}, {day_offset: 2, laundry: 60}]`
- **THEN** the laundry sensor state is `85` and `extra_state_attributes["forecast"]` is `[{"day_offset": 1, "score": 72}, {"day_offset": 2, "score": 60}]`

#### Scenario: Single-day data produces empty forecast
- **WHEN** enrichment data contains only `days[0]` with no subsequent days
- **THEN** the activity sensor state is `days[0]`'s score and `extra_state_attributes["forecast"]` is an empty list `[]`

#### Scenario: Forecast updates via stream
- **WHEN** a new enrichment event arrives with updated index data containing different day scores
- **THEN** the sensor state and forecast attribute are both updated to reflect the new values

### Requirement: DayScoreData model
The integration SHALL define a `DayScoreData` frozen dataclass representing one day's activity scores, with fields: `day_offset` (int), `laundry` (int), `outdoor` (int), `running` (int), `cycling` (int), `bbq` (int), `solar` (int), `night_ventilation` (int), `hours_included` (int).

#### Scenario: DayScoreData defaults
- **WHEN** a `DayScoreData()` is created with no arguments
- **THEN** all int fields default to `0`

### Requirement: FrostData model
The integration SHALL define a `FrostData` frozen dataclass with fields: `hours_until` (int) and `confidence` (float).

#### Scenario: FrostData from proto
- **WHEN** proto `FrostInfo` contains `hours_until_frost = 4` and `confidence = 0.85`
- **THEN** `FrostData(hours_until=4, confidence=0.85)` is produced

### Requirement: VpdData model
The integration SHALL define a `VpdData` frozen dataclass with fields: `kpa` (float) and `category` (str).

#### Scenario: VpdData from proto
- **WHEN** proto `VpdInfo` contains `kpa = 0.59` and `category = "optimal"`
- **THEN** `VpdData(kpa=0.59, category="optimal")` is produced
