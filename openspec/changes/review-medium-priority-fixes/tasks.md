## 1. BudgetTracker Snapshot Message Fix

- [x] 1.1 In `src/Njord.Budget/BudgetTracker.cs`, change `Receive<DeleteSnapshotSuccess>` to `Receive<DeleteSnapshotsSuccess>` (plural)

## 2. Polling Telemetry Fix

- [x] 2.1 In `src/Njord.Polling/Telemetry.cs`, rename the 3 counters from `njord_pipeline_*` to `njord_polling_*`
- [x] 2.2 In the same file, change the `DomainKind.Forecast` tag mapping from `"weather"` to `"forecast"`

## 3. Location Name Uniqueness

- [x] 3.1 In the NjordOptions validator, add a check that rejects duplicate location names (case-insensitive)
- [x] 3.2 Add a test for the duplicate name validation

## 4. Dead Config Cleanup

- [x] 4.1 In `src/Njord/appsettings.Development.json`, remove the dead `"Weather"` block and replace with correct `"Forecast"` section if needed

## 5. Package Version Alignment

- [x] 5.1 In `src/Directory.Packages.props` -- reverted: 1.5.73 does not exist for Akka.Persistence.Sql.Hosting (separate release cycle, 1.5.70 is latest stable), bump `Akka.Persistence.Sql.Hosting` from 1.5.70 to 1.5.73

## 6. Model Comparison Domain Requirements

- [x] 6.1 In `src/Njord.Features/Forecasts/ForecastModelComparison/ForecastModelComparison.cs`, add `Forecast` to `DomainRequirements` so the rule only triggers on Forecast domain updates
- [x] 6.2 In `src/Njord.Features/Forecasts/EnsembleModelComparison/EnsembleModelComparison.cs`, add `Ensemble` to `DomainRequirements` so the rule only triggers on Ensemble domain updates
- [x] 6.3 Update existing model comparison tests to verify evaluation is skipped for non-matching domains (not needed -- FeatureRunner bypasses domain dispatch; the FeatureEngine dispatch loop handles domain filtering via existing code paths)

## 7. Shared Materializer in WeatherGrpcService

- [x] 7.1 In `src/Njord.Grpc/WeatherGrpcService.cs`, inject or cache a shared `IMaterializer` and use it in streaming methods instead of creating per-call

## 8. Validation

- [x] 8.1 Run `dotnet build Njord.slnx` from `src/`
- [x] 8.2 Run all test projects
- [x] 8.3 Run `dotnet format Njord.slnx --verify-no-changes` from `src/`
