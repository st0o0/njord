## 1. Foundation

- [x] 1.1 Add `prometheus-net.AspNetCore` to `src/Directory.Packages.props` and `src/Njord/Njord.csproj`
- [x] 1.2 Create `src/Njord/Diagnostics/NjordMetrics.cs` — sealed class, private constructor, static `Instance`, `Meter` property named `"Njord"`
- [x] 1.3 Wire `/metrics` endpoint in `src/Njord/Program.cs` — `app.UseHttpMetrics()` and `app.MapMetrics()`, enable .NET runtime metrics
- [x] 1.4 Create `src/Njord.Tests/Diagnostics/NjordMetricsSpec.cs` — verify singleton identity, Meter name, instrument creation via extensions

## 2. Ingest Metrics

- [x] 2.1 Create `src/Njord/Diagnostics/IngestMetricsExtensions.cs` — `AddFetchTotal()` → `Counter<long>`, `AddFetchDuration()` → `Histogram<double>`
- [x] 2.2 Instrument `src/Njord/Ingest/OpenMeteoClient.cs` — record fetch outcome + duration after `FetchAsync`
- [x] 2.3 Create `src/Njord.Tests/Diagnostics/IngestMetricsExtensionsSpec.cs` — verify instrument names, units, and that labels are recorded correctly using `MeterListener`

## 3. Budget Metrics

- [x] 3.1 Extend `src/Njord/Health/NjordHealthState.cs` with `BudgetUsedDaily`, `BudgetUsedMonthly`, `BudgetLimitDaily`, `BudgetLimitMonthly` properties (thread-safe)
- [x] 3.2 Update `src/Njord/Pipeline/BudgetTrackerActor.cs` to write budget values to `NjordHealthState` on each acquisition
- [x] 3.3 Create `src/Njord/Diagnostics/BudgetMetricsExtensions.cs` — `AddBudgetUsedDaily(Func<long>)`, `AddBudgetUsedMonthly(Func<long>)`, `AddBudgetLimitDaily(Func<long>)`, `AddBudgetLimitMonthly(Func<long>)` → ObservableGauges; `AddThrottleWait()` → `Histogram<double>`
- [x] 3.4 Instrument `src/Njord/Pipeline/BudgetThrottleStage.cs` — record throttle wait duration on timer-based resume
- [x] 3.5 Register budget ObservableGauges in startup (reading from `NjordHealthState`)
- [x] 3.6 Create `src/Njord.Tests/Diagnostics/BudgetMetricsExtensionsSpec.cs` — verify gauge callbacks and throttle histogram

## 4. Pipeline Metrics

- [x] 4.1 Create `src/Njord/Diagnostics/PipelineMetricsExtensions.cs` — `AddPollCycleDuration()` → `Histogram<double>`, `AddPollCycleModels()` → `Gauge`, `AddDataChanged()` → `Counter<long>`
- [x] 4.2 Instrument `src/Njord/Pipeline/SchedulerActor.cs` — record poll cycle duration and model count on cycle-complete, increment data-changed on hash change
- [x] 4.3 Create `src/Njord.Tests/Diagnostics/PipelineMetricsExtensionsSpec.cs` — verify instrument names, units, label recording

## 5. Enrichment Metrics

- [x] 5.1 Create `src/Njord/Diagnostics/EnrichmentMetricsExtensions.cs` — `AddEnrichmentDuration()` → `Histogram<double>`, `AddConsensusModels()` → `Gauge`, `AddConsensusSpread()` → `Gauge`, `AddHistoryMae()` → `Gauge`, `AddHistoryModelWeight()` → `Gauge`
- [x] 5.2 Instrument `src/Njord/Enrichment/EnrichmentActor.cs` — record enrichment duration per feature, set consensus gauges after consensus result
- [x] 5.3 Instrument `src/Njord/Enrichment/Features/HistoryEnrichment.cs` (or `ForecastHistoryActor`) — set MAE and model weight gauges after history computation
- [x] 5.4 Create `src/Njord.Tests/Diagnostics/EnrichmentMetricsExtensionsSpec.cs` — verify instrument names, units, label recording

## 6. Egress Metrics

- [x] 6.1 Create `src/Njord/Diagnostics/EgressMetricsExtensions.cs` — `AddMqttDedup()` → `Counter<long>`, `AddMqttConnected()` → `Gauge`
- [x] 6.2 Instrument `src/Njord/Mqtt/MqttEgressActor.cs` — increment dedup counter with `published`/`skipped` decision at existing dedup point
- [x] 6.3 Instrument `src/Njord/Mqtt/MqttConnectionActor.cs` — set mqtt connected gauge on connect/disconnect
- [x] 6.4 Create `src/Njord.Tests/Diagnostics/EgressMetricsExtensionsSpec.cs` — verify instrument names, units, label recording

## Validation

```powershell
dotnet build src/Njord.slnx
dotnet run --project src/Njord.Tests/Njord.Tests.csproj
dotnet slopwatch
dotnet format src/Njord.slnx --verify-no-changes
```
