## 1. Guard first

- [ ] 1.1 Add `NamingArchitectureSpec` (no production type name contains `Snapshot`; no duplicate simple names between `Njord.Messages` and other production assemblies) and confirm it fails on the current names

## 2. Renames (each group builds and tests green before the next)

- [ ] 2.1 Health: `MqttHealthSnapshot`, `PollHealthSnapshot`, `BudgetHealthSnapshot` to `MqttHealth`, `PollHealth`, `BudgetHealth`
- [ ] 2.2 Config: `ConfigSnapshot` to `EffectiveConfig`, `ConfigSnapshotMappings` to `EffectiveConfigMappings`; rename the DTO `ParameterSelection` to `ParameterScope` and remove the alias and qualified-name workarounds
- [ ] 2.3 Sensors: `SensorSnapshot`, `SensorSnapshots`, `QuerySensorSnapshot`, `SensorSnapshotResponse/Result/NotAvailable`, `RequestSensorSnapshots` to the `SensorReadings` family
- [ ] 2.4 `ModelSnapshot` to `ModelForecasts` and `ConsensusSnapshot` to `LocationConsensus`
- [ ] 2.5 `GetSnapshot()` mappers to `ToPollStateEntry()` and `ToBudgetUsage()`; rename members, parameters and test helpers that carry the old word

## 3. Files, docs and specs

- [ ] 3.1 Rename files and Verify golden-master files that embed old type names (`git mv`, content unchanged)
- [ ] 3.2 Update `AGENTS.md`, `docs/` and the 25 main specs textually (names only); keep Akka terms; add the naming rule to `AGENTS.md` conventions
- [ ] 3.3 Grep for leftover `Snapshot`/`snapshot` in production code and make sure only Akka terms and `Persisted<Name>State` documentation remain

## 4. Verification

- [ ] 4.1 `NamingArchitectureSpec` passes; run `dotnet format whitespace`, build `Njord.slnx`, run every test project, `openspec validate --all --no-interactive`, `scripts/check-openspec-root.sh`; golden masters and wire-format specs unchanged
