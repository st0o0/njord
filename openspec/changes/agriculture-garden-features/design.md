## Context

njord has five unused Forecast variables (Evapotranspiration, VapourPressureDeficit, SoilTemperature, SnowDepth partial, SoilMoisture0To1cm partial). Existing SoilMoisture and Evapotranspiration features expose raw data as simple level classifications. This change adds three new features that combine these variables into actionable decisions: IrrigationNeed (water/skip), FrostDepth (frost depth estimate), SprayWindow (optimal spray timing).

The existing feature pattern is well-established: `FeatureRule<TInput>` + `<Name>Config : IConfigSection` + result record + proto message + gRPC mapping + registration in `AddFeatures`.

## Goals / Non-Goals

**Goals:**
- Three new features in `Njord.Features` using only Forecast domain data
- Actionable outputs (enums for decisions, not raw numbers) with numeric attributes for power users
- Follow existing patterns exactly (SoilMoisture/Evapotranspiration as reference)
- SprayWindow uses `ScoreFeatureResult` ready for the score-windows TimeWindow extension

**Non-Goals:**
- HA sensor input (real soil sensors, irrigation controller feedback)
- Crop-specific models, precision agriculture
- Regulatory spray compliance (weather suitability only)

## Decisions

### 1. IrrigationNeed uses a decision matrix, not a score

IrrigationNeed outputs an `IrrigationAdvice` enum (Water/Skip/Reduce/SoilWet) rather than a numeric score. Gardeners need "should I water?" not "your irrigation score is 67". The decision matrix checks soil moisture thresholds first (fast exit for wet/cold), then balances ET loss against expected rain.

**Why not a score:** Scores imply gradation that doesn't match irrigation reality — you either water or you don't. The Reduce case covers "rain coming but not enough."

### 2. FrostDepth uses simplified Stefan model

The Stefan equation `d = k × √(FDH)` is the standard frost-depth estimator in civil engineering. We use k ≈ 2.0 (empirical for temperate soils) with soil-type and wind modifiers. This is explicitly an estimate — real frost depth depends on soil moisture, organic content, and groundwater depth that we don't have.

**Why not a threshold alert:** FrostDepth is categorically different from a frost alert (temp < 0). It estimates *how deep* frost goes, which matters for pipes and roots. A pipe at 60cm doesn't care about surface frost.

### 3. SprayWindow uses ScoreFeatureResult with future TimeWindow support

SprayWindow produces a `ScoreFeatureResult(Score, BestStart, BestHours)` today. When the score-windows change lands, it gains `repeated TimeWindow` for top-N windows with LimitingFactor. The feature calculates per-hour scores now; the window extraction will use the shared `WindowCalculator` from score-windows.

Until score-windows ships, SprayWindow works like Laundry/Outdoor/Bbq: one best window via `BestStart`/`BestHours`.

**Why not wait for score-windows:** The per-hour scoring logic and the feature shell are independent. The window extraction is additive.

### 4. All three use ForecastInput (single-domain)

All features need only Forecast data. They use the existing `ForecastInput(DomainForecast<ForecastPoint>)` rather than a new input type. The `DomainRequirements` flags declare which specific Forecast variables they need.

### 5. IrrigationNeed and FrostDepth share SoilType enum

Both features need a soil type classification. We define a single `SoilType` enum in `Njord.Messages` (or alongside the feature if scoping is preferred) and reference it from both configs. The enum affects different calculations in each feature (moisture thresholds vs. thermal conductivity).

### 6. Category is Agriculture for all three

New `FeatureCategory.Agriculture` value. Config paths: `Njord:Features:Agriculture:IrrigationNeed`, `Njord:Features:Agriculture:FrostDepth`, `Njord:Features:Agriculture:SprayWindow`.

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| SoilMoisture 0-1cm is a coarse proxy for root-zone moisture | Document as surface indicator; IrrigationNeed explicitly provides advice, not precision measurement |
| Frost depth model accuracy varies with soil composition | Conservative `pipe_risk` threshold at 70% of pipe depth; document as estimate |
| SprayWindow without score-windows only has one window | Still useful as "best time today"; score-windows adds multi-window later |
| FeatureCategory.Agriculture is new | Requires adding to the enum; trivial change |
