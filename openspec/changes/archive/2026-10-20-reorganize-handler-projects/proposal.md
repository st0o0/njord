## Why

The four handler projects are organized by data source (Weather, AirQuality, Flood, Marine), but handlers are classified by output pattern (`OutputKind`). A score handler could live in any of the four projects; finding it requires knowing which API its data happens to come from. Seven of 22 handlers already read from multiple domains via `LocationForecast`, so data-source grouping is arbitrary. As the system grows (Hiking-Score, Dust-Alert, Sailing-Conditions, Drought-Forecast…), a source-based split will not scale — new features land in whichever project "feels closest", and the largest project (Weather: 29 files mixing alerts, scores, conditions, trends, forecasts and shared helpers) keeps growing.

## What Changes

- **BREAKING (internal)**: delete the four handler projects (`Njord.Weather`, `Njord.AirQuality`, `Njord.Flood`, `Njord.Marine`) and their test projects.
- Create four new handler projects grouped by `OutputKind` pattern:
  - **`Njord.Alerts`** — all `IAlertRule`-based handlers, rules, `AlertWindow`, `AirQualityAlertWindow`, `WeatherAlertHandler` (multi-feature), `AirQualityAlertHandler`, `FloodRiskHandler`, `FloodRainHandler`, `SwimmingConditionsHandler`, and their rules (14 weather rules, 2 AQ rules, 2 flood rules, 1 marine rule). Also `FloodRainInput`.
  - **`Njord.Scores`** — window-score handlers (`BBQScoreHandler`, `LaundryScoreHandler`, `OutdoorScoreHandler`), `Scoring` helpers, `SurfConditionsHandler` + `SurfConditionsRule` (score-based condition with a 0-100 rating).
  - **`Njord.Insights`** — data passthrough and trend handlers: `SeasonalOutlookHandler`, `SolarYieldHandler`, `SolarIrradianceBlend`, `SatelliteRadiationHandler`, `ConfidenceTrendHandler`, `ApproachingFrontHandler`, `PollenTrendHandler`.
  - **`Njord.Advisories`** — condition-check handlers: `CommuteSafetyHandler`, `VentilationWindowHandler`, `NightVentilationHandler`, `ExerciseAdvisoryHandler`, `GardeningHandler`, `FloodTimelineHandler`.
- Each new project gets a matching test project (`Njord.Alerts.Tests`, `Njord.Scores.Tests`, `Njord.Insights.Tests`, `Njord.Advisories.Tests`).
- Registration extension methods renamed: `AddAlertHandlers()`, `AddScoreHandlers()`, `AddInsightHandlers()`, `AddAdvisoryHandlers()`. Called from the Host.
- `ArchitectureSpec` updated for the new project names (same guardrails: handler projects reference only Core and below, never each other, never Host).
- Solution file (`Njord.slnx`) updated.
- `AGENTS.md` structure section updated.

## Capabilities

### New Capabilities
- `handler-project-structure`: The new project layout, what lives where, registration conventions, and the mapping from old to new locations.

### Modified Capabilities
- `structural-refactoring`: Project rename and file-move rules apply to this change.

## Non-goals

- No new features, handlers, or alert rules.
- No handler logic changes beyond namespace and `using` updates.
- No changes to Core, Messages, Persistence, Pipeline, OpenMeteo, Grpc, Budget, Polling, or Host logic.
- No `OutputKind` changes.
- No changes to the feature toggle system, wire keys, or gRPC contract.

## API-budget impact

None. No polling, request, or variable changes.

## Impact

- `src/Njord.Weather/`, `src/Njord.AirQuality/`, `src/Njord.Flood/`, `src/Njord.Marine/` — deleted.
- `src/Njord.Weather.Tests/`, `src/Njord.AirQuality.Tests/`, `src/Njord.Flood.Tests/`, `src/Njord.Marine.Tests/` — deleted.
- `src/Njord.Alerts/`, `src/Njord.Scores/`, `src/Njord.Insights/`, `src/Njord.Advisories/` — new.
- `src/Njord.Alerts.Tests/`, `src/Njord.Scores.Tests/`, `src/Njord.Insights.Tests/`, `src/Njord.Advisories.Tests/` — new.
- `src/Njord.slnx` — project references updated.
- `src/Njord/` (Host) — registration calls renamed.
- `src/Njord.Architecture.Tests/` — zone rules updated.
- `AGENTS.md` — structure section updated.
