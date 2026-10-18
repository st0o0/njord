## 1. Enum mappings

- [ ] 1.1 Add specs pinning every `AlertType` and `AlertSeverity` member and an undefined value for `ToProto()` in `WireEnumMappingsSpec`
- [ ] 1.2 Move `MapAlertType` and `MapAlertSeverity` into `WireEnumMappings` as throwing `ToProto()` extensions and update call sites
- [ ] 1.3 Add `ToTimestamp(this DateTimeOffset)` and replace every private and inline Timestamp conversion in `Njord.Grpc`

## 2. gRPC mapping split

- [ ] 2.1 Split `AnalysisProtoMappings` into alert, index, trend, derived and consensus mapping classes plus `EnrichmentEventMappings`; drop `Map*` names
- [ ] 2.2 Split `ConfigProtoMappings` into `ConfigRequestMappings` and `ConfigResponseMappings`; move `Applied/Validated/Rejected/Rethrow` to a `ConfigResponses` helper
- [ ] 2.3 Replace the nested `PollFields` in `OpsProtoMappings` with a plain private helper or shared extension
- [ ] 2.4 Split the existing mapping specs to match and verify the gRPC golden masters are unchanged

## 3. Config and options mappings

- [ ] 3.1 Rename `ConfigSnapshotMapping` to `ConfigSnapshotMappings` and turn the private helpers into `ToSection()`-style extensions with specs
- [ ] 3.2 Move `ToMessage(PollingOptions)` and `BuildAnalysisUpdates` from `ConfigManager` into `ConfigUpdateMappings` with specs
- [ ] 3.3 Add `ParametersOptions.Resolve()` and use it in the container and in `NjordOptionsValidator`
- [ ] 3.4 Add `PollingOptions.ToInitialBudgetSnapshot()` with a spec

## 4. Health and containers

- [ ] 4.1 Give `NjordHealthMonitor` a constructor and remove the `init` property; update all callers and specs
- [ ] 4.2 Move the budget gauge registration into `BudgetSetupContainer`; add a spec that the gauges follow monitor updates
- [ ] 4.3 Expose `MeterName` constants on the `Telemetry` classes and derive the meter allow-list from them
- [ ] 4.4 Split `CoreSetupContainer` into `CoreSetupContainer`, `ConfigSetupContainer` and `HealthSetupContainer`; register them in `Program.cs`; adjust container specs

## 5. Enforcement and docs

- [ ] 5.1 Add `MappingConventionArchitectureSpec` (static internal `*Mappings`, no instance state, no private `Map*` builders outside mapping classes)
- [ ] 5.2 Update `AGENTS.md` (conventions, solution structure, container list) and `docs/development` where the mapping files are named
- [ ] 5.3 Run `dotnet format whitespace`, build `Njord.slnx`, run every test project, `openspec validate --all --no-interactive`, `scripts/check-openspec-root.sh`
