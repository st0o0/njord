## Context

Four handler projects organized by data source contain 44 non-generated source files (Weather 29, AirQuality 6, Flood 6, Marine 3) with 22 handlers, 19 alert rules, and shared helpers. The handlers fall into five patterns by `OutputKind`: Alert (5 handlers + 19 rules), Score (3 handlers + 1 condition-as-score), Condition (8 handlers), Trend (2 handlers), Forecast/passthrough (3 handlers). Registration lives in per-project `ServiceCollectionExtensions` called from the Host.

## Goals / Non-Goals

**Goals:**
- Reorganize handler projects by output pattern so handlers are findable by what they do.
- Preserve every handler, rule, helper, test, and registration without behavioral changes.
- Keep the architecture guardrails (handler projects reference only Core and below).

**Non-Goals:**
- No handler logic changes, no new shared base classes, no deduplication of alert handler boilerplate (future work).
- No `OutputKind` or `FeatureCategory` changes.
- No changes to non-handler projects (Core, Messages, Persistence, Pipeline, OpenMeteo, Grpc, Budget, Polling).

## Decisions

### 1. Four projects, not five

Merging Conditions and Trends into a single project avoids a two-handler project (`ConfidenceTrend`, `PollenTrend`) that would grow slowly. The remaining question is the name.

**Chosen: `Njord.Advisories`**. An advisory is actionable guidance: "ventilate now", "commute is dangerous", "pollen is rising". This fits both condition checks and trend analyses. Rejected alternatives: `Njord.Analysis` (too vague, sounds like data processing), `Njord.Conditions` (excludes trends conceptually).

### 2. Surf goes into Scores, not Advisories

`SurfConditionsHandler` emits `OutputKind.Condition` but computes a 0-100 score, has a `RatingFor()` classification, and shares the window-scoring pattern with BBQ/Laundry/Outdoor. It belongs with the scoring pattern. `SurfConditionsRule` (the static evaluator) moves with it.

### 3. Insights absorbs passthrough + trend-like insight handlers

`SeasonalOutlookHandler` (Forecast), `SolarYieldHandler` (Condition), `SatelliteRadiationHandler` (Condition), `SolarIrradianceBlend` (helper), `ConfidenceTrendHandler` (Trend), `ApproachingFrontHandler` (Condition), and `PollenTrendHandler` (Condition) all share a pattern: they transform domain data into informational output. They don't score, don't alert, don't advise action. "Insights" captures this: observatory data that helps understanding. The `OutputKind` values vary (Forecast, Condition, Trend) but the intent is uniform.

### 4. File move mapping

| Old location | New project | Namespace |
|---|---|---|
| `Njord.Weather/WeatherAlertHandler.cs` | `Njord.Alerts` | `Njord.Alerts` |
| `Njord.Weather/AlertWindow.cs` | `Njord.Alerts` | `Njord.Alerts` |
| `Njord.Weather/Rules/*.cs` (14 files) | `Njord.Alerts/Rules/` | `Njord.Alerts.Rules` |
| `Njord.AirQuality/AirQualityAlertHandler.cs` | `Njord.Alerts` | `Njord.Alerts` |
| `Njord.AirQuality/AirQualityAlertWindow.cs` | `Njord.Alerts` | `Njord.Alerts` |
| `Njord.AirQuality/PollenAlertRule.cs` | `Njord.Alerts` | `Njord.Alerts` |
| `Njord.AirQuality/Pm25AlertRule.cs` | `Njord.Alerts` | `Njord.Alerts` |
| `Njord.Flood/FloodRiskHandler.cs` | `Njord.Alerts` | `Njord.Alerts` |
| `Njord.Flood/FloodRiskAlertRule.cs` | `Njord.Alerts` | `Njord.Alerts` |
| `Njord.Flood/FloodRainHandler.cs` | `Njord.Alerts` | `Njord.Alerts` |
| `Njord.Flood/FloodRainAlertRule.cs` | `Njord.Alerts` | `Njord.Alerts` |
| `Njord.Flood/FloodRainInput.cs` | `Njord.Alerts` | `Njord.Alerts` |
| `Njord.Marine/SwimmingConditionsHandler.cs` | `Njord.Alerts` | `Njord.Alerts` |
| `Njord.Marine/SwimmingConditionsRule.cs` | `Njord.Alerts` | `Njord.Alerts` |
| `Njord.Weather/Lifestyle/BBQScoreHandler.cs` | `Njord.Scores` | `Njord.Scores` |
| `Njord.Weather/Lifestyle/LaundryScoreHandler.cs` | `Njord.Scores` | `Njord.Scores` |
| `Njord.Weather/Lifestyle/OutdoorScoreHandler.cs` | `Njord.Scores` | `Njord.Scores` |
| `Njord.Weather/Lifestyle/Scoring.cs` | `Njord.Scores` | `Njord.Scores` |
| `Njord.Marine/SurfConditionsHandler.cs` | `Njord.Scores` | `Njord.Scores` |
| `Njord.Marine/SurfConditionsRule.cs` | `Njord.Scores` | `Njord.Scores` |
| `Njord.Weather/Lifestyle/SeasonalOutlookHandler.cs` | `Njord.Insights` | `Njord.Insights` |
| `Njord.Weather/Lifestyle/SolarYieldHandler.cs` | `Njord.Insights` | `Njord.Insights` |
| `Njord.Weather/Lifestyle/SolarIrradianceBlend.cs` | `Njord.Insights` | `Njord.Insights` |
| `Njord.Weather/Lifestyle/SatelliteRadiationHandler.cs` | `Njord.Insights` | `Njord.Insights` |
| `Njord.Weather/Lifestyle/ConfidenceTrendHandler.cs` | `Njord.Insights` | `Njord.Insights` |
| `Njord.Weather/Lifestyle/ApproachingFrontHandler.cs` | `Njord.Insights` | `Njord.Insights` |
| `Njord.AirQuality/PollenTrendHandler.cs` | `Njord.Insights` | `Njord.Insights` |
| `Njord.Weather/Lifestyle/CommuteSafetyHandler.cs` | `Njord.Advisories` | `Njord.Advisories` |
| `Njord.Weather/Lifestyle/VentilationWindowHandler.cs` | `Njord.Advisories` | `Njord.Advisories` |
| `Njord.Weather/Lifestyle/NightVentilationHandler.cs` | `Njord.Advisories` | `Njord.Advisories` |
| `Njord.Weather/Lifestyle/ExerciseAdvisoryHandler.cs` | `Njord.Advisories` | `Njord.Advisories` |
| `Njord.Weather/Lifestyle/GardeningHandler.cs` | `Njord.Advisories` | `Njord.Advisories` |
| `Njord.Flood/FloodTimelineHandler.cs` | `Njord.Advisories` | `Njord.Advisories` |

### 5. Namespace strategy

Flat namespaces per project: `Njord.Alerts`, `Njord.Scores`, `Njord.Insights`, `Njord.Advisories`. The only subfolder is `Njord.Alerts/Rules/` (namespace `Njord.Alerts.Rules`) because 14 weather rules + 5 others benefit from grouping. No `Lifestyle/` subfolder in any project.

### 6. Project files

Each `.csproj` copies the structure of the existing handler projects: `<ProjectReference Include="..\Njord.Core\Njord.Core.csproj" />`, no other Njord references. `RootNamespace` matches the project name. The test `.csproj` files reference the new project and `Njord.Tests.Shared`.

### 7. Registration extensions

Each project exposes a `ServiceCollectionExtensions` class (namespace = project namespace) with a single extension method:
- `Njord.Alerts` → `AddAlertHandlers()`: registers all 19 `IAlertRule<T>` singletons and all 5 `IDomainHandler` singletons (including the `AirQualityAlertHandler` factory registrations).
- `Njord.Scores` → `AddScoreHandlers()`: registers BBQ, Laundry, Outdoor, Surf as `IDomainHandler`.
- `Njord.Insights` → `AddInsightHandlers()`: registers Seasonal, SolarYield, SolarIrradiance (blend is not registered; it's a static helper), Satellite, ConfidenceTrend, ApproachingFront, PollenTrend as `IDomainHandler`.
- `Njord.Advisories` → `AddAdvisoryHandlers()`: registers Commute, Ventilation, NightVentilation, Exercise, Gardening, FloodTimeline as `IDomainHandler`.

Host replaces `AddWeatherDomain()`, `AddAirQualityDomain()`, `AddFloodDomain()`, `AddMarineDomain()` with the four new calls.

### 8. Test mapping

Tests follow their handlers. The test file mapping mirrors the production mapping. Existing fakes and builders in `Njord.Tests.Shared` are not moved. The test count (Weather 122 + AirQuality 20 + Flood 28 + Marine 63 = 233) SHALL be preserved across the four new test projects.

### 9. Execution order

Phase 1: Create new projects and `.csproj` files in parallel.
Phase 2: Move files, update namespaces and `using` directives.
Phase 3: Update Host registration, `ArchitectureSpec`, solution file.
Phase 4: Delete old projects.
Phase 5: Build, format, run all tests.
Phase 6: Update `AGENTS.md`.

Within each phase, work proceeds project-by-project (Alerts first — largest — then Scores, Insights, Advisories).

## Risks / Trade-offs

- **Git history**: `git log --follow` tracks renames, but moves across projects lose some history. Mitigated by a single commit with clear message.
- **Merge conflicts with `expand-air-quality`**: if that change adds new AQ handlers or rules before this lands, they need manual placement. Mitigated by landing `expand-air-quality` first.
- **Naming ambiguity of Insights vs Advisories**: "Is PollenTrend an insight or an advisory?" Answer: it informs (rising/falling), it doesn't advise action. If future handlers blur the line, the move is cheap.
