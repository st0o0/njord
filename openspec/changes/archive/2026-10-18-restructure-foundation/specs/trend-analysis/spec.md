## REMOVED Requirements

### Requirement: ForecastSeries-based PrecipitationTiming formula
**Reason**: `TrendFormulas.PrecipitationTiming(ForecastSeries, ParameterDef, DateTimeOffset)` is dead code — never called from production. The `TrendAnalyzer` has its own Consensus-based `PrecipitationTiming` implementation that supersedes it.
**Migration**: No migration needed. The Consensus-based `TrendAnalyzer.PrecipitationTiming` (private method) provides identical behavior over `LocationConsensus` data.

### Requirement: ForecastSeries-based ExtremaTiming formula
**Reason**: `TrendFormulas.ExtremaTiming(ForecastSeries, ParameterDef, DateTimeOffset)` is dead code — never called from production. The `TrendAnalyzer` has its own Consensus-based `ExtremaTiming` implementation that supersedes it.
**Migration**: No migration needed. The Consensus-based `TrendAnalyzer.ExtremaTiming` (private method) provides identical behavior over `LocationConsensus` data.
