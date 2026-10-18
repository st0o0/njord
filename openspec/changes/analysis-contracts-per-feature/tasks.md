## 1. Spec reconciliation and guards

- [ ] 1.1 Write the MODIFIED/REMOVED deltas for `compute-library` and `architecture-zone-enforcement` against the final project names and add them to this change
- [ ] 1.2 Extend `LayerReferenceSpec` with the rules for `Njord.Consensus` and the four contracts projects (failing first)

## 2. Skeleton

- [ ] 2.1 Create `Njord.Alerts.Contracts`, `Njord.Derived.Contracts`, `Njord.Trends.Contracts`, `Njord.Indices.Contracts`, add them to `Njord.slnx`, `Dockerfile` and CI paths, reference them from Messages and Core

## 3. Move per feature (build, tests and golden masters green after each)

- [ ] 3.1 Derived: `DerivedResult`, `HorizonDerived`, `ScalarDerived`, `DewPointComfort`, `VpdCategory`, `VpdInfo`, `DerivedSettings` and related records
- [ ] 3.2 Trends: `TrendResult`, `ParameterTrend`, `TrendDirection`, `Stability`, `StabilityInfo`, `TrendSettings`
- [ ] 3.3 Alerts: `Alert*`, `SeverityThresholds`, `AlertTypeExtensions`, `AlertSettings`
- [ ] 3.4 Indices: `Index*`, `DayScoreSet`, `ScoreEnvelope`, `ResolvedPreferences`, `ScanDirection`, `IndexSettings`
- [ ] 3.5 Move `WeatherCondition`, `WeatherCategory` to `Njord.Weather`; place `AnalysisSettings` next to `AnalysisSettingsChanged` in Messages

## 4. Rename Compute to Consensus

- [ ] 4.1 Rename project, namespace and test project to `Njord.Consensus` / `Njord.Consensus.Tests`; remove the empty `Njord.Compute`
- [ ] 4.2 Move specs with their types; add contracts test projects only where contract logic exists

## 5. Enforcement and docs

- [ ] 5.1 Make the new `LayerReferenceSpec` and `ZoneArchitectureSpec` rules pass; update `AGENTS.md` (solution structure, reference direction, test counts) and `docs/`
- [ ] 5.2 Run `dotnet format whitespace`, build `Njord.slnx`, run every test project, `openspec validate --all --no-interactive`, `scripts/check-openspec-root.sh`
