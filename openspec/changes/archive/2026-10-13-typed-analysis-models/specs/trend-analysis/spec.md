## MODIFIED Requirements

### Requirement: Trend direction compares consensus median between snapshots

Trend analysis SHALL accept `ConsensusSnapshot` and `ConsensusSnapshot?` previous instead of `ModelSnapshot`. Trend direction compares consensus medians at the same horizon between two consecutive snapshots and SHALL be a typed direction (Rising, Falling, Stable). Its published text SHALL be "rising", "falling" or "stable", unchanged from before.

#### Scenario: Rising trend
- **WHEN** current consensus median temperature at +3 h is 22 degrees C and previous was 19 degrees C
- **THEN** the trend direction is Rising with delta 3.0

#### Scenario: Falling trend
- **WHEN** current consensus median temperature at +3 h is 15 degrees C and previous was 19 degrees C
- **THEN** the trend direction is Falling with delta -4.0

#### Scenario: Stable within dead-band
- **WHEN** the delta between current and previous consensus median is within the dead-band
- **THEN** the trend direction is Stable

#### Scenario: Null previous
- **WHEN** `previous` is null
- **THEN** no trend events are produced

#### Scenario: Null current
- **WHEN** the current consensus median is null at a horizon
- **THEN** that horizon's trend is null

### Requirement: Weather-change detection compares WMO code categories
Weather-change detection SHALL accept an `int?` previous WMO code and an `int?` current WMO code. It SHALL classify each code into a typed weather category: Clear (0-3), Fog (45-48), Drizzle (51-57), Rain (61-67), Snow (71-77), Showers (80-86), Thunderstorm (95-99), and Unknown for any other code (published as "unknown"). If the category changed, it SHALL return a weather-change result with the typed `From` and `To` categories. The published text SHALL be unchanged: lowercase category names and the description "<from> → <to>" (e.g. "clear → rain"), produced at the output edge. If the category did not change or either code is `null`, the result SHALL be `null`.

#### Scenario: Clear to rain
- **WHEN** previous WMO code is 1 (mainly clear) and current is 63 (moderate rain)
- **THEN** the result has From=Clear and To=Rain, published as "clear", "rain" and "clear → rain"

#### Scenario: Same category
- **WHEN** previous WMO code is 61 (slight rain) and current is 65 (heavy rain)
- **THEN** the result is null (both are Rain)

#### Scenario: Null codes
- **WHEN** either previous or current WMO code is null
- **THEN** the result is null

### Requirement: Consensus stability compares IQR between snapshots

Stability SHALL compare IQR values from `ConsensusSnapshot.Hourly` between current and previous and SHALL be a typed stability (Converging, Diverging, Stable), published as "converging", "diverging" or "stable".

#### Scenario: Converging models
- **WHEN** current IQR is smaller than previous IQR
- **THEN** stability is Converging

#### Scenario: Diverging models
- **WHEN** current IQR is larger than previous IQR
- **THEN** stability is Diverging

#### Scenario: Stable
- **WHEN** IQR ratio is within tolerance
- **THEN** stability is Stable

## REMOVED Requirements

### Requirement: TrendResult serialization with pinned wire names
**Reason**: Trend result records are in-process values only; nothing serializes or persists them.
**Migration**: The published wire format is pinned by `wire-format-golden-masters`. The named records that replaced value tuples stay.
