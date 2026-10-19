## 1. Remove Consensus

- [x] 1.1 Delete `src/Njord.Consensus/` project and `src/Njord.Consensus.Tests/`
- [x] 1.2 Remove from solution and project references
- [x] 1.3 Move any reusable query logic to ForecastQueries if not already there

## 2. Remove Derived

- [x] 2.1 Delete `src/Njord.Derived/` project and `src/Njord.Derived.Tests/`
- [x] 2.2 Remove from solution and project references

## 3. Remove Forecast project

- [x] 3.1 Delete `src/Njord.Forecast/` project and `src/Njord.Forecast.Tests/`
- [x] 3.2 Remove from solution and project references
- [x] 3.3 Any types still needed (if any) have been replaced by DomainForecast<WeatherPoint>

## 4. Decide and remove Trends/Indices

- [x] 4.1 If Trends not migrated: delete `src/Njord.Trends/` and `src/Njord.Trends.Tests/`
- [x] 4.2 If Indices not migrated: delete `src/Njord.Indices/` and `src/Njord.Indices.Tests/`
- [x] 4.3 Remove from solution and project references

## 5. Remove old types

- [x] 5.1 Remove `CycleId` from `src/Njord.Model/`
- [ ] 5.2 Remove `LocationId` from `src/Njord.Model/` (replaced by Location) -- deferred: still the name-only identity of ~35 message/query records
- [x] 5.3 Remove `ModelForecasts` and old pipeline state types
- [x] 5.4 Remove old PipelineEvent variants (ForecastReceived, ConsensusComputed, etc.)
- [x] 5.5 Clean up Njord.Messages — remove empty folders

## 6. Update architecture and documentation

- [x] 6.1 Rewrite `LayerReferenceSpec` for new project structure
- [x] 6.2 Rewrite `ZoneArchitectureSpec` for new zones
- [x] 6.3 Update `NjordArchitecture` assembly list
- [x] 6.4 Rewrite CLAUDE.md solution structure section
- [x] 6.5 Rewrite AGENTS.md architecture description
- [x] 6.6 Update test count in CLAUDE.md

## 7. Validation

- [x] 7.1 `dotnet build Njord.slnx`
- [x] 7.2 Full test suite passes
- [x] 7.3 `openspec validate --all --no-interactive` passes
