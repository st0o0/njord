## REMOVED Requirements

### Requirement: ForecastHistoryActor persists forecast records via Akka.Persistence
**Reason**: Historical learning was removed: there is no `Njord.History` project, no `ForecastHistoryActor`, `HistoryAnalyzer`, `HistoryResult` or `HistoryOptions`, and `EnrichmentKind` (src/Njord.Messages/Pipeline/EnrichmentKind.cs) has no history member.
**Migration**: None. A future history feature would be proposed as a new analysis library under its own change.

### Requirement: ForecastHistoryActor responds to history queries
**Reason**: Historical learning was removed: there is no `Njord.History` project, no `ForecastHistoryActor`, `HistoryAnalyzer`, `HistoryResult` or `HistoryOptions`, and `EnrichmentKind` (src/Njord.Messages/Pipeline/EnrichmentKind.cs) has no history member.
**Migration**: None. A future history feature would be proposed as a new analysis library under its own change.

### Requirement: Model accuracy tracking via MAE
**Reason**: Historical learning was removed: there is no `Njord.History` project, no `ForecastHistoryActor`, `HistoryAnalyzer`, `HistoryResult` or `HistoryOptions`, and `EnrichmentKind` (src/Njord.Messages/Pipeline/EnrichmentKind.cs) has no history member.
**Migration**: None. A future history feature would be proposed as a new analysis library under its own change.

### Requirement: Weighted consensus from inverse MAE
**Reason**: Historical learning was removed: there is no `Njord.History` project, no `ForecastHistoryActor`, `HistoryAnalyzer`, `HistoryResult` or `HistoryOptions`, and `EnrichmentKind` (src/Njord.Messages/Pipeline/EnrichmentKind.cs) has no history member.
**Migration**: None. A future history feature would be proposed as a new analysis library under its own change.

### Requirement: Forecast drift as run-to-run standard deviation
**Reason**: Historical learning was removed: there is no `Njord.History` project, no `ForecastHistoryActor`, `HistoryAnalyzer`, `HistoryResult` or `HistoryOptions`, and `EnrichmentKind` (src/Njord.Messages/Pipeline/EnrichmentKind.cs) has no history member.
**Migration**: None. A future history feature would be proposed as a new analysis library under its own change.

### Requirement: Seasonal preference identifies best model per season
**Reason**: Historical learning was removed: there is no `Njord.History` project, no `ForecastHistoryActor`, `HistoryAnalyzer`, `HistoryResult` or `HistoryOptions`, and `EnrichmentKind` (src/Njord.Messages/Pipeline/EnrichmentKind.cs) has no history member.
**Migration**: None. A future history feature would be proposed as a new analysis library under its own change.

### Requirement: Anomaly detection via z-score
**Reason**: Historical learning was removed: there is no `Njord.History` project, no `ForecastHistoryActor`, `HistoryAnalyzer`, `HistoryResult` or `HistoryOptions`, and `EnrichmentKind` (src/Njord.Messages/Pipeline/EnrichmentKind.cs) has no history member.
**Migration**: None. A future history feature would be proposed as a new analysis library under its own change.

### Requirement: HistoryResult aggregates all history analysis and serializes to MQTT
**Reason**: Historical learning was removed: there is no `Njord.History` project, no `ForecastHistoryActor`, `HistoryAnalyzer`, `HistoryResult` or `HistoryOptions`, and `EnrichmentKind` (src/Njord.Messages/Pipeline/EnrichmentKind.cs) has no history member.
**Migration**: None. A future history feature would be proposed as a new analysis library under its own change.
