## Why

Njord.Weather has grown into a grab bag of 20 files spanning four distinct responsibilities: vocabulary types used by every project (ParameterDef, Horizon, WeatherModel), time-series containers used only by ingest and pipeline (Forecast, ForecastSeries, ModelForecasts), compute functions (HorizonProjection, ForecastDataHash), and a config DTO (LocationOptions). Every project in the solution references it, but most only need the vocabulary types. The flat `.slnx` grouping (/Foundation/, /Domain/) no longer reflects the actual architecture, and several ForecastSeries-based methods in Derived and Trends are dead code — superseded by Consensus-based implementations in the analyzers. Additionally, `DailyForecastPoint` carries two dictionaries (NumericValues, MetaValues) as a workaround for two string-typed parameters (sunrise/sunset), complicating every consumer.

Restructuring now — before adding more features — prevents the coupling from getting worse and makes the zone boundaries enforceable at the project-reference level, not just by ArchUnit convention.

## What Changes

- **Remove dead methods**: `ForecastSeries.Mean()`, `.Values()`, `.Window()`; `DerivedFormulas.DiurnalAmplitude(ForecastSeries)`, `.SunshinePercent(ForecastSeries)`; `TrendFormulas.PrecipitationTiming(ForecastSeries)`, `.ExtremaTiming(ForecastSeries)` — plus their tests. These are unused in production, superseded by Consensus-based implementations in the analyzer classes.
- **Introduce `ForecastValue`**: A unified value type replacing `double?` (hourly) and the dual `NumericValues`/`MetaValues` dictionaries (daily). `ForecastPoint` and `DailyForecastPoint` converge to a single dictionary signature. `HorizonValue` becomes a thin wrapper over `ForecastValue`.
- **Split Njord.Weather** into two projects:
  - **Njord.Model** (zero dependencies): `ParameterDef`, `ParameterRegistry`, `WeatherModel`, `LocationId`, `Horizon`, `CycleId`, `WeatherCondition`, `ResolvedParameterSet`, `ForecastValue`.
  - **Njord.Forecast** (→ Njord.Model): `Forecast`, `ForecastSeries`, `ForecastPoint`, `DailyForecastSeries`, `DailyForecastPoint`, `ModelForecasts`, `HorizonProjection`, `TimeAnchor`, `ForecastDataHash`, `PollOutcome`, `HorizonValue`.
- **Move `LocationOptions`** from Weather to `Njord.Core/Configuration/` (config DTO, not domain).
- **Full namespace rename**: `Njord.Weather` → `Njord.Model` (vocabulary) and `Njord.Forecast` (time-series) across ~80 production files and ~50 test files.
- **Update ProjectReferences**: Consensus → Model + Forecast; Messages → Model + Sensors + Consensus + Forecast; Core → Model + Forecast + Messages + Persistence + Consensus. Analysis features keep referencing only Core — they never see Njord.Forecast.
- **Reorganize `.slnx` solution folders** into 9 groups reflecting the actual architecture zones.
- **Update ArchUnit specs**: `LayerReferenceSpec` (new reference rules), `ZoneArchitectureSpec` (zone assignments), `NamingArchitectureSpec`, and `DocumentationArchitectureSpec` (test counts, structure documentation).
- **Update CLAUDE.md**: Solution structure section, reference direction, zone descriptions.

## Non-goals

- Changing the `ForecastPoint` dictionary-based design to a typed-property or column-oriented approach — the dictionary design is the right trade-off for configurable parameter sets.
- Moving types between Consensus, Messages, Persistence, or Core (beyond LocationOptions) — those projects are already well-scoped.
- Physical directory reorganization (e.g., `src/Model/Njord.Model/`) — projects stay flat under `src/`, only `.slnx` logical folders change.
- Any persistence format changes — `Njord.Persistence` is completely unaffected.
- Performance optimizations (FrozenDictionary for ForecastPoint, etc.) — separate concern.
- Changing how polling, the pipeline, or analysis features work — this is pure structure, no behavior change.

## Capabilities

### New Capabilities

- `forecast-value-type`: Unified value type (`ForecastValue`) replacing the dual numeric/meta dictionary pattern in daily forecast points and the separate `HorizonValue` type.
- `model-forecast-split`: Project split of Njord.Weather into Njord.Model (vocabulary, zero-dependency) and Njord.Forecast (time-series data, → Model only). Includes namespace rename, ProjectReference rewiring, and LocationOptions relocation.
- `solution-zone-structure`: Nine-zone `.slnx` folder reorganization (/Model/, /Forecast/, /Contracts/, /Infrastructure/, /Ingest/, /Analysis/, /Processing/, /Egress/, /Tests/) with updated ArchUnit zone enforcement and CLAUDE.md documentation.

### Modified Capabilities

- `derived-values`: Removal of dead `ForecastSeries`-based formulas (`DiurnalAmplitude`, `SunshinePercent`) that duplicate the Consensus-based implementations in `DerivedAnalyzer`.
- `trend-analysis`: Removal of dead `ForecastSeries`-based formulas (`PrecipitationTiming`, `ExtremaTiming`) that duplicate the Consensus-based implementations in `TrendAnalyzer`.

## Impact

- **All production projects**: `using Njord.Weather` → `using Njord.Model` or `using Njord.Forecast` (mechanical namespace rename).
- **All test projects**: Same namespace rename, plus tests for dead methods are removed, ForecastValue adoption changes test construction patterns for `ForecastPoint` and `DailyForecastPoint`.
- **Njord.slnx**: Complete folder restructure (9 folders replacing 3).
- **CI**: No script changes needed (projects stay flat under `src/`, `dotnet build Njord.slnx` and the test loop work unchanged).
- **Njord.Architecture.Tests**: `LayerReferenceSpec` and `ZoneArchitectureSpec` need new rules for Model, Forecast, and the zone assignments.
- **CLAUDE.md / AGENTS.md**: Solution structure section, reference direction, convention documentation.
- **No API budget impact**: This change does not add or alter polling behavior.
