## Why

A full project review identified seven medium-priority issues: incorrect message handling, misleading telemetry names, missing config validation, dead config, a package version skew, unnecessary rule evaluations, and per-call materializer allocation. All are low-effort fixes that improve correctness and observability.

## What Changes

- Fix BudgetTracker to handle `DeleteSnapshotsSuccess` (plural) instead of `DeleteSnapshotSuccess` (singular)
- Rename 3 Polling telemetry counters from `njord_pipeline_*` to `njord_polling_*` and fix the Forecast domain tag from `"weather"` to `"forecast"` (**BREAKING** metric names)
- Add Location.Name uniqueness validation in `NjordOptionsValidator`
- Remove dead `Njord:Weather` config block from `appsettings.Development.json`
- Align `Akka.Persistence.Sql.Hosting` to 1.5.73 with other Akka packages
- Add minimal `DomainRequirements` to ForecastModelComparison and EnsembleModelComparison so they only evaluate on relevant domain updates
- Cache a shared `IMaterializer` in `WeatherGrpcService` instead of creating one per streaming call

## Capabilities

### New Capabilities

- `review-cleanup`: Collection of correctness, observability and config hygiene fixes from the project review

### Modified Capabilities

(none)

## Non-goals

- Changing BudgetTracker persistence logic beyond the message handler fix
- Adding new telemetry instruments
- Changing the model comparison feature logic itself
- Expanding integration test coverage (separate effort)

## Impact

- `src/Njord.Budget/BudgetTracker.cs`: one message type fix
- `src/Njord.Polling/Telemetry.cs`: rename 3 counters + 1 tag
- `src/Njord.Core/Configuration/NjordOptionsValidator.cs` (or wherever the validator lives): add name uniqueness
- `src/Njord/appsettings.Development.json`: remove dead block
- `src/Directory.Packages.props`: version bump
- `src/Njord.Features/Forecasts/ForecastModelComparison/ForecastModelComparison.cs`: add requirements
- `src/Njord.Features/Forecasts/EnsembleModelComparison/EnsembleModelComparison.cs`: add requirements
- `src/Njord.Grpc/WeatherGrpcService.cs`: inject materializer
