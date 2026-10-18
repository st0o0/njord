## REMOVED Requirements

### Requirement: ForecastSeries-based DiurnalAmplitude formula
**Reason**: `DerivedFormulas.DiurnalAmplitude(ForecastSeries, ParameterDef, DateTimeOffset)` is dead code — never called from production. The `DerivedAnalyzer` has its own Consensus-based `Amplitude` implementation that supersedes it.
**Migration**: No migration needed. The Consensus-based `DerivedAnalyzer.Amplitude` (private method) provides identical behavior over `LocationConsensus` data.

### Requirement: ForecastSeries-based SunshinePercent formula
**Reason**: `DerivedFormulas.SunshinePercent(ForecastSeries, ParameterDef, ParameterDef, DateTimeOffset)` is dead code — never called from production. The `DerivedAnalyzer` has its own Consensus-based `SunshinePercent` implementation that supersedes it.
**Migration**: No migration needed. The Consensus-based `DerivedAnalyzer.SunshinePercent` (private method) provides identical behavior over `LocationConsensus` data.
