## Context

Njord.Weather (20 files, 0 dependencies) is the most-referenced project in the solution (~80 production files import it). It contains four categories of types that have different stability profiles and consumer sets:

1. **Vocabulary types** (used everywhere): `ParameterDef`, `ParameterRegistry`, `WeatherModel`, `LocationId`, `Horizon`, `CycleId`, `WeatherCondition`, `ResolvedParameterSet`
2. **Time-series containers** (used by ingest + pipeline + consensus only): `Forecast`, `ForecastSeries`, `ForecastPoint`, `DailyForecast*`, `ModelForecasts`
3. **Compute functions** (used by pipeline/consensus/mqtt): `HorizonProjection`, `TimeAnchor`, `ForecastDataHash`
4. **Misplaced types**: `LocationOptions` (config DTO), `PollOutcome` (fetch result)

Additionally, `DailyForecastPoint` carries two dictionaries (`NumericValues`, `MetaValues`) as a workaround for 2 of 17 daily parameters being strings (sunrise/sunset). And several `ForecastSeries`-based methods in `DerivedFormulas` and `TrendFormulas` are dead code, superseded by Consensus-based implementations in the analyzers.

The `.slnx` groups projects into `/Foundation/`, `/Domain/`, and `/Tests/` — which no longer reflects the actual five-tier architecture (vocabulary → forecast → contracts → infrastructure → features).

## Goals / Non-Goals

**Goals:**
- Split Weather into two projects with clear responsibilities and minimal dependency surfaces
- Make the "analysis features never touch raw forecasts" rule enforceable at the project-reference level
- Remove dead code that creates false coupling (ForecastSeries-based formulas)
- Unify the hourly/daily forecast point APIs via ForecastValue
- Reorganize solution folders to match the real architecture
- Update ArchUnit rules to enforce the new structure

**Non-Goals:**
- Changing ForecastPoint's dictionary-based design to typed properties or column-oriented storage
- Performance optimizations (FrozenDictionary, array-indexed lookups)
- Touching Persistence records or persistence format
- Changing runtime behavior of any component

## Decisions

### D1: Four-phase execution order

**Decision**: Execute in strict order — dead code removal → ForecastValue → project split → solution folders + ArchUnit.

**Rationale**: Each phase reduces the surface area for the next. Dead code removal shrinks the files that reference `ForecastSeries` (removing 4 methods + their tests before the split). ForecastValue unifies the ForecastPoint/DailyForecastPoint APIs before they move to a new namespace, avoiding doing the unification and the move simultaneously. The project split is the big change; doing it on a cleaner base reduces merge conflicts and review noise.

**Alternatives considered**:
- All at once: Too risky for review; a namespace rename + API change + project split in one commit is unreadable.
- Split first, then clean up: Would move dead code to a new project only to delete it.

### D2: ForecastValue as a readonly record struct

**Decision**: `ForecastValue` is a `readonly record struct` with `double? Number` and `string? Text`, implicit conversions from `double?` and `string?`, and value equality.

```
public readonly record struct ForecastValue(double? Number, string? Text)
{
    public bool HasValue => Number.HasValue || Text is not null;
}
```

**Rationale**: A struct avoids heap allocation for the ~2800 values per forecast. Implicit conversions let existing code like `dict[param] = 22.5` keep working. The struct is 16 bytes (8 for nullable double + 8 for string reference), same as a nullable double + string pair.

**Alternatives considered**:
- Discriminated union (`OneOf<double, string>`): Overkill — there are exactly two value shapes, and they're well-known.
- Keep `double?` for hourly, `ForecastValue` only for daily: Defeats the purpose of unification; every consumer would need to know which point type it has.

### D3: ConsensusBuilder stays in Njord.Consensus

**Decision**: `ConsensusBuilder` remains in `Njord.Consensus`, which gains a reference to `Njord.Forecast` (in addition to `Njord.Model`).

**Rationale**: ConsensusBuilder semantically belongs to consensus — it builds consensus. That it reads forecast data to do so is an honest dependency. Moving it to Pipeline (the only caller) would put domain logic in an orchestration project. Moving it to Forecast would create a circular-in-spirit dependency (forecast producing consensus types).

**Alternatives considered**:
- Move to Pipeline: Puts computation in orchestration; misnames the responsibility.
- Move to Forecast: Forecast would need to know about Consensus types — wrong direction.

### D4: ResolvedParameterSet stays in Model (not Forecast)

**Decision**: `ResolvedParameterSet` moves to `Njord.Model`, not `Njord.Forecast`.

**Rationale**: It describes *which parameters are active* — a configuration-level concept built from `ParameterDef`. It has no knowledge of forecasts, time series, or data points. Its `ApiCallWeight` is a pure function of the hourly parameter count. It's used by Core (ParameterOptions), Consensus (ConsensusBuilder), OpenMeteo (client/gateway), and Mqtt (state builder) — all of which reference Model anyway.

### D5: HorizonValue goes to Forecast

**Decision**: `HorizonValue` moves to `Njord.Forecast`, not `Njord.Model`.

**Rationale**: It is created by `HorizonProjection` (a Forecast type) and consumed by Mqtt's `StatePayloadBuilder` and `ForecastPayloadMappings`. It represents a *projected value at a horizon* — a concept that only exists after forecast data has been processed. Mqtt gets it transitively through Core.

### D6: LocationOptions moves to Core/Configuration

**Decision**: `LocationOptions` moves from Weather to `Njord.Core/Configuration/LocationOptions.cs`.

**Rationale**: It's a mutable options DTO (`{ get; set; }` properties, used with IConfiguration binding). It has nothing to do with the domain model. It sits alongside `NjordOptions` which already references it, and `PollingOptions`, `OpenMeteoOptions` etc.

### D7: Feature libraries keep their transitive-only access pattern

**Decision**: Feature libraries (Alerts, Derived, Trends, Indices, OpenMeteo, Budget, SensorHub, Mqtt, Grpc, Pipeline) continue to reference only `Njord.Core`. They receive `Njord.Model` and `Njord.Forecast` types transitively. No direct `<ProjectReference>` to Model or Forecast.

**Rationale**: This is how it works today (everything references Core, which references Weather). Keeping it avoids adding 20 new ProjectReference lines and makes the "only Core and below" rule simpler. If a feature library needs a Model type, it already gets it through Core. The ArchUnit rules enforce that no feature library bypasses Core.

### D8: Flat physical layout, logical-only solution folders

**Decision**: All projects stay under `src/Njord.*/`. Only `.slnx` solution folders change.

**Rationale**: Physical subdirectories (`src/Model/Njord.Model/`) would change every `<ProjectReference>` path, every `dotnet run --project` command, the CI test loop, and CLAUDE.md's documented paths. The IDE provides the visual grouping through solution folders. `ls src/` showing all projects flat is fine for ~20 projects.

### D9: Namespace rename strategy

**Decision**: Full rename — `namespace Njord.Weather` → `namespace Njord.Model` or `namespace Njord.Forecast`. Every `using Njord.Weather` statement is updated.

**Rationale**: A project named `Njord.Model` with namespace `Njord.Weather` would confuse every developer. The rename is mechanical (search-replace), touches many files but changes no logic. `PipelineEvent.cs` has a `using WeatherForecast = Njord.Weather.Forecast` alias that becomes `using WeatherForecast = Njord.Forecast.Forecast` — still needed to disambiguate from the `Njord.Messages.Forecast` namespace.

### D10: ForecastSeries becomes a minimal container

**Decision**: After removing `Mean()`, `Values()`, and `Window()`, `ForecastSeries` retains only its `Points` property and constructor (sort-on-construct). It becomes a pure data container.

**Rationale**: The query methods were never used in production. ConsensusBuilder iterates `Points` directly. Keeping ForecastSeries as a thin ordered-list wrapper is correct — it guarantees temporal ordering, which is its only invariant.

## Risks / Trade-offs

**[Risk: Large diff across many files]** → The namespace rename touches ~130 files. Mitigation: Phase 3 (the split) is a single mechanical commit that can be reviewed by verifying the build passes and grepping for residual `Njord.Weather` references. Each phase is a separate commit.

**[Risk: Messages namespace collision]** → `Njord.Messages` has a `Forecast/` folder (namespace `Njord.Messages.Forecast`). The new `Njord.Forecast` project has namespace `Njord.Forecast`. These are different namespaces and don't collide. The existing `using WeatherForecast = Njord.Weather.Forecast` alias in `PipelineEvent.cs` becomes `using WeatherForecast = Njord.Forecast.Forecast` — needed to distinguish the record from the namespace. Verify this compiles.

**[Risk: Test count changes]** → Removing dead formula tests reduces the test count. `DocumentationArchitectureSpec` will fail until the count in CLAUDE.md is updated. Mitigation: Update the count in Phase 1 (after dead code removal) and again in Phase 3 (after split adds new test project).

**[Risk: ForecastValue changes all ForecastPoint construction sites]** → Every test that constructs `new ForecastPoint(time, dict)` needs its dictionary values to be `ForecastValue` instead of `double?`. Mitigation: Implicit conversion from `double?` to `ForecastValue` means most construction sites work unchanged. `DailyForecastPoint` tests change more (two dicts → one), but there are only ~20 construction sites in tests.

**[Trade-off: Consensus gains a Forecast dependency]** → Previously Consensus depended only on Weather (which contained everything). Now it depends on Model + Forecast — two references instead of one, but each is precisely scoped. The total transitive surface is smaller (Model has no compute, Forecast has no config).
