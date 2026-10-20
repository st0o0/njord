## Context

The IDomainHandler pattern makes adding enrichments trivial: declare Triggers, implement Handle, register in DI. Each handler is ~50 lines of pure logic with no framework overhead.

## Goals / Non-Goals

**Goals:** Seven actionable lifestyle enrichments that justify njord's multi-domain architecture.
**Non-Goals:** Consumer wiring for the new events, configurable thresholds.

## Decisions

### D1: Cross-domain handlers live in Njord.Weather
VentilationWindow and ExerciseAdvisory need both Weather and AirQuality. Rather than a new Njord.Lifestyle project, they live in Njord.Weather with `Triggers = {Weather, AirQuality}`. The AQ dependency is declared, not structural.

### D2: Scoring helper for weighted composites
A small `Scoring` static class in Njord.Weather normalizes values to 0–100 sub-scores. Used by LaundryScore and ExerciseAdvisory.

### D3: ForecastQueries.Upcoming for time-windowed access
New utility: `Upcoming(forecast, now, hours)` returns points from now to now+hours. Used by all handlers to scan the relevant time window.
