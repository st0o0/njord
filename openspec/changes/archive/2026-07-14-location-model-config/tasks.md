## 1. LocationOptions and model resolution

- [x] 1.1 Add `IList<string>? Models` property to LocationOptions
- [x] 1.2 Add `ResolveModels(IList<string> globalModels)` method — merges, deduplicates
- [x] 1.3 Write LocationOptionsSpec — 4 tests

## 2. ModelCoverageRegistry

- [x] 2.1 Create ModelCoverageRegistry — CoverageTier enum, BoundingBox record, ModelCoverage record, static dictionary
- [x] 2.2 Populate bounding boxes for all documented Open-Meteo models (19 global, 9 Europe, 20+ regional)
- [x] 2.3 Write ModelCoverageRegistrySpec — 11 tests

## 3. Startup validation

- [x] 3.1 Update NjordOptionsValidator — resolve per-location models, validate coverage, fail on out-of-coverage
- [x] 3.2 Update budget calculation — sum per-location model counts
- [x] 3.3 Coverage validation tested via existing validator tests + new registry tests

## 4. Actor updates

- [x] 4.1 SchedulerActor.InitializeStates — iterate `location.ResolveModels(_options.Models)`
- [x] 4.2 DiscoveryActor.PublishDiscovery — iterate `location.ResolveModels(_options.Models)`
- [x] 4.3 Existing scheduler tests pass (no direct model iteration testing needed)

## 5. Configuration

- [x] 5.1 Update appsettings.json — Borken + Winterswijk with per-location models
- [x] 5.2 Global Models still works when no location-specific Models set

## 6. Validation

- [x] 6.1 Unit tests: 424 pass (15 new)
- [x] 6.2 Integration tests: 7 pass
- [x] 6.3 Build: 0 errors
- [x] 6.4 Slopwatch: 2 pre-existing warnings only
