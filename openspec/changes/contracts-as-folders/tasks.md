## 1. Guards first

- [ ] 1.1 Extend `LayerReferenceSpec` and `ZoneArchitectureSpec` with the rules for `Njord.Consensus` and the Messages guard (no `*Analyzer`, `*Rule`, `*Formulas`, no Akka), failing first

## 2. Move contracts into Messages folders (build, tests and golden masters green after each)

- [ ] 2.1 Derived: result records, `DewPointComfort`, `VpdCategory`, `DerivedSettings` into `Njord.Messages/Derived`
- [ ] 2.2 Trends: `TrendResult`, `ParameterTrend`, `TrendDirection`, `Stability`, `StabilityInfo`, `TrendSettings` into `Njord.Messages/Trends`
- [ ] 2.3 Alerts: `Alert*`, `SeverityThresholds`, `AlertTypeExtensions`, `AlertSettings` into `Njord.Messages/Alerts`
- [ ] 2.4 Indices: `Index*`, `DayScoreSet`, `ScoreEnvelope`, `ResolvedPreferences`, `IndexSettings` into `Njord.Messages/Indices`
- [ ] 2.5 Move `WeatherCondition`, `WeatherCategory` to `Njord.Weather` and `AnalysisSettings` to `Njord.Messages/Config`

## 3. Rename Compute to Consensus

- [ ] 3.1 Rename project, namespace and test project to `Njord.Consensus` / `Njord.Consensus.Tests`; remove the empty `Njord.Compute`
- [ ] 3.2 Move contract specs to the new `Njord.Messages.Tests`; add it to `Njord.slnx`

## 4. Enforcement and docs

- [ ] 4.1 Make the new rules pass; update `Dockerfile` `COPY` lines, CI paths, `AGENTS.md` (solution structure, reference direction, test totals) and `docs/`

## 5. Verification

- [ ] 5.1 `dotnet format whitespace`, build `Njord.slnx` with 0 warnings, run every test project (golden masters unchanged), `openspec validate --all --no-interactive`, `scripts/check-openspec-root.sh`
