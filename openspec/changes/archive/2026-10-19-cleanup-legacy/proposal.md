## Why

After the pipeline and polling are redesigned, several old projects become dead code: Njord.Consensus (replaced by Ensemble API), Njord.Derived (replaced by API-native values), Njord.Forecast (replaced by DomainForecast<WeatherPoint>), Njord.Trends and Njord.Indices (decision: migrate as ProjectionGroups or remove). This change removes the dead code and consolidates the project structure.

## What Changes

- Remove `Njord.Consensus` project (ConsensusBuilder, ConsensusStatistics, queries, settings).
- Remove `Njord.Derived` project (DerivedAnalyzer, DerivedFormulas).
- Remove `Njord.Forecast` project (replaced by DomainForecast<WeatherPoint>).
- Remove `Njord.Trends` project (or migrate as a ProjectionGroup if keeping).
- Remove `Njord.Indices` project (or migrate as a ProjectionGroup if keeping).
- Remove old `PipelineEvent` types (ForecastReceived, ConsensusComputed, DerivedComputed, TrendComputed, IndexComputed).
- Remove corresponding test projects.
- Update architecture tests for new project structure.
- Remove `CycleId`, `LocationId` (replaced by `Location`), `ModelForecasts`.
- Update CLAUDE.md and AGENTS.md for new architecture.

## Non-goals

- Adding new features — purely removal and consolidation.
- API budget impact: zero.

## Capabilities

### New Capabilities

_(none — removal only)_

### Modified Capabilities

- `consensus-computation`: Entirely removed (replaced by Ensemble API).
- `derived-values`: Entirely removed (API-native).
- `trend-analysis`: Removed or migrated.
- `activity-indices`: Removed or migrated as ProjectionGroup.
- `architecture-zone-enforcement`: Updated for new project structure.

## Impact

- **Solution**: ~5-7 projects removed, ~5-7 test projects removed.
- **Test count**: Significant reduction (~300+ tests removed).
- **Architecture tests**: Zone rules, layer references, naming conventions all updated.
- **Documentation**: Major CLAUDE.md/AGENTS.md rewrite for new architecture.
