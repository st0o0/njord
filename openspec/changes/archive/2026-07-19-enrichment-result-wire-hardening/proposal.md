## Why

The `EnrichmentSnapshotActor` stores enrichment results as nested JSON inside `EnrichmentEntryDto.JsonPayload`. While the outer DTO has pinned `[JsonProperty]` wire names (from the recent persistence-DTO work), the inner domain records (`AlertResult`, `IndexResult`, `TrendResult`, `DerivedResult`, `EnergyResult`, `ConsensusResult` and their nested types) use Newtonsoft.Json's default PascalCase serialization with no pinned property names. Renaming any property on these records silently breaks snapshot recovery. Additionally, several records use C# value tuples (`(int HoursUntilFrost, double Confidence)?`) which serialize as `Item1`/`Item2` — fragile and unreadable wire names that cannot be pinned with `[JsonProperty]`.

## What Changes

- Add `[property: JsonProperty("...")]` attributes to all enrichment result record positional parameters and their nested types (6 root records, ~8 nested records).
- Replace all C# value tuples in enrichment result records with small named records so their properties can be pinned with `[JsonProperty]`.
- Add Verify snapshot tests for the `EnrichmentSnapshotMapping` round-trip to lock the wire format of the nested JSON.
- Remove the CLAUDE.md caveat about the EnrichmentEntryDto inner-JSON limitation (gap closed).

## Non-goals

- No changes to the `EnrichmentSnapshotDto`/`EnrichmentEntryDto` outer structure or the type-dispatch mechanism (`TypeName` + `JsonPayload`).
- No changes to the `EnrichmentSnapshotMapping` class itself — only the types it serializes.
- No polling changes — zero API-budget impact.

## Capabilities

### New Capabilities

_(none)_

### Modified Capabilities

- `snapshot-persistence-dtos`: Enrichment result records gain pinned wire names, closing the inner-JSON gap documented in the persist DTO extend-only rules.
- `threshold-alerts`: `Alert` record gains `[JsonProperty]` + tuple fields replaced with named records.
- `activity-indices`: `IndexResult` and `ScoreEnvelope` gain `[JsonProperty]` + tuple fields replaced.
- `trend-analysis`: `TrendResult`, `ParameterTrend`, `WeatherChangeResult` gain `[JsonProperty]` + tuple fields replaced.
- `derived-values`: `DerivedResult`, `HorizonDerived`, `ScalarDerived` gain `[JsonProperty]`.
- `energy-management`: `EnergyResult` gains `[JsonProperty]` + tuple fields replaced.
- `consensus-computation`: `ConsensusResult`, `ParameterConsensus`, `HorizonConsensus` gain `[JsonProperty]` + tuple fields replaced.

## Impact

- **Domain records** (`src/Njord/Domain/Analysis/`): All 6 result record files + nested types get `[JsonProperty]` attributes. Value tuples replaced with named records — callers that destructure tuples must update.
- **Tests**: Any test constructing enrichment results with tuple syntax must update to use the new named record types.
- **Wire format**: Pre-production, no existing snapshots. The wire format changes (PascalCase → pinned names, `Item1`/`Item2` → named fields) but there is no data to migrate.
- **Dependencies**: No new packages — `Newtonsoft.Json` is already referenced.
