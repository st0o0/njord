## Context

The `EnrichmentSnapshotActor` persists enrichment results via `EnrichmentEntryDto`, which stores each result as a `TypeName` + `JsonPayload` pair. The outer DTO is hardened with `[JsonProperty]`, but the inner serialization of domain records uses Newtonsoft.Json defaults — PascalCase property names and `Item1`/`Item2` for value tuples. This is the last unhardened persistence surface in the project.

There are 6 root result records and ~8 nested types across `src/Njord/Domain/Analysis/`. Several use C# value tuples that cannot carry `[JsonProperty]` attributes. The project is pre-production with no existing snapshot data.

## Goals / Non-Goals

**Goals:**
- Pin wire names on all enrichment result records and their nested types via `[JsonProperty]`
- Replace value tuples with named records so all serialized properties have pinned wire names
- Add Verify snapshot tests locking the nested JSON wire format
- Remove the CLAUDE.md caveat about this gap

**Non-Goals:**
- No changes to `EnrichmentEntryDto`, `EnrichmentSnapshotDto`, or `EnrichmentSnapshotMapping`
- No migration code (pre-production, no data)
- No refactoring of the type-dispatch mechanism (`TypeName` → `Type` dictionary)

## Decisions

### 1. `[property: JsonProperty]` on positional record parameters

**Decision:** Use `[property: JsonProperty("camelCase")]` target syntax on positional record parameters.

**Why:** C# records define properties via constructor parameters. The `property:` target is required for attributes to apply to the generated property rather than the constructor parameter. Wire names use camelCase to match the JSON convention already used by the outer DTOs.

**Alternative considered:** Creating separate DTO classes for each enrichment result (like `ForecastRecordDto`). Rejected — would require a mapping layer per result type (6 root + 8 nested = 14 classes + 14 mappers), massive overhead for types that are already simple data records with no domain logic.

### 2. Replace value tuples with named records

**Decision:** Each value tuple becomes a small `sealed record` in the same file as its parent.

**Why:** Value tuples serialize as `Item1`/`Item2` under Newtonsoft.Json — the named fields are compile-time only. Named records can carry `[JsonProperty]` and produce readable, stable wire names.

Affected tuples and their replacements:

| Parent | Tuple | Replacement Record |
|--------|-------|--------------------|
| `IndexResult` | `(int HoursUntilFrost, double Confidence)?` | `FrostProtectionInfo` |
| `IndexResult` | `(string Category, double Vpd)?` | `VpdInfo` |
| `TrendResult` | `(int? StartsInHours, int? EndsInHours)` | `PrecipTimingInfo` |
| `TrendResult` | `(int? MaxInHours, int? MinInHours)` | `ExtremaTimingInfo` |
| `TrendResult` | `(string Label, double Ratio)?` | `StabilityInfo` |
| `TrendResult` | `(double DecayRate, int? ReliableHours)?` | `DecayInfo` |
| `EnergyResult` | `(int HoursFromNow, double Cop)` (in list) | `CopOptimalEntry` |
| `HorizonConsensus` | `(WeatherModel Model, double Deviation)?` | `OutlierInfo` |
| `HorizonConsensus` | `(double Lower, double Upper)?` | `ConfidenceIntervalInfo` |

### 3. Wire name convention

**Decision:** Use camelCase for all `[JsonProperty]` names, matching property names lowercased. Keep names descriptive (not abbreviated like the outer DTOs), since these are domain records used beyond just persistence.

**Why:** The outer DTOs use short abbreviated names (`v`, `loc`, `ts`) because they're pure wire types. These domain records are used throughout the codebase — readable property names matter more than byte savings.

## Risks / Trade-offs

- **Callers that destructure tuples must update** → All call sites and test code constructing tuples need to use the new named records. Grep for each tuple type to find all sites. Risk is low since the compiler will catch every instance.
- **`[JsonProperty]` on domain records mixes concerns** → Accepted trade-off. The alternative (full DTO layer for 14 types) is disproportionate to the risk. These records ARE the serialization surface for `EnrichmentEntryDto.JsonPayload`.
- **`ParameterDef` and `WeatherModel` in `ConsensusResult`** → These domain types appear as properties in `ParameterConsensus` and `HorizonConsensus`. They need `[JsonProperty]` too, but only on properties that are actually serialized. `WeatherModel.Id` is the only property; `ParameterDef` has several. Both already serialize correctly via Newtonsoft defaults, but pinning protects against renames.
