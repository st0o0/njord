## 1. Domain enums and mappings (TDD, no wire change yet)

- [x] 1.1 `src/Njord.Compute/Contracts/VpdCategory.cs`: enum + `ToWireName()`; `VpdInfo.Category` becomes `VpdCategory`; `src/Njord.Indices/VaporPressureDeficit.cs` returns it; update `Njord.Indices.Tests` and `Njord.Compute.Tests` (exact category per threshold band)
- [x] 1.2 `src/Njord.Mqtt/StatePayloadBuilder.cs`: VPD category via `ToWireName()`, alert severity via `AlertSeverity.ToWireName()` (add the extension in `Njord.Compute/Contracts`); `Njord.Mqtt.Tests` snapshots stay byte-identical
- [x] 1.3 `src/Njord.OpenMeteo/OpenMeteoClient.cs`: explicit outcome-tag switch with the same label values; spec in `Njord.OpenMeteo.Tests` asserting every outcome's label
- [x] 1.4 Named constants for the sensor source `"default"` (`Njord.Grpc/Mapping/SensorProtoMappings.cs`) and the MQTT `"unknown"` location fallback (`Njord.Mqtt/MqttStatePublisher.cs`)

## 2. Proto enums

- [x] 2.1 Write failing mapping specs in `Njord.Grpc.Tests` for every new enum (all members, UNSPECIFIED/absent, undefined value throws)
- [x] 2.2 `protos/njord/v2/common.proto`: enums `TrendDirection`, `Stability`, `DewPointComfort`, `VpdCategory`; replace `ParameterTrend.direction`, `stability_label`, `HorizonDerived.dewpoint_comfort`, `VpdInfo.category` (new field numbers, old `reserved`)
- [x] 2.3 `protos/njord/v2/ops.proto`: enum `PollPhase`; replace `ModelStatus.phase` and `TriggerTarget.phase`
- [x] 2.4 `protos/njord/v2/admin.proto`: enum `ConsensusMethod` (`MEAN`, `MEDIAN`, `TRIMMED_MEAN`); replace `ConsensusConfig.method`
- [x] 2.5 `src/Njord.Grpc/Mapping/{Analysis,Ops}ProtoMappings.cs`: exhaustive switches to the proto enums (throwing default)
- [x] 2.6 `src/Njord.Grpc/Mapping/ConfigProtoMappings.cs`: explicit `ConsensusMethod` mapping both directions; `SetEnrichment` UNSPECIFIED leaves unchanged, undefined value gives Rejected; specs in `Njord.Grpc.Tests`
- [x] 2.7 Regenerate/update golden masters in `Njord.Grpc.Tests` and review the diff for the changed fields only

## 3. Docs and specs

- [x] 3.1 Update `docs/` pages that show phase, direction, stability, dew point comfort, VPD category or consensus method values for gRPC
- [x] 3.2 Add note on unknown-enum tolerance to the gRPC docs and `AGENTS.md` (Decisions or Conventions)

## 4. Validation

- [x] 4.1 `dotnet build Njord.slnx` and the sequential test loop from `AGENTS.md` (all projects green; `Njord.Architecture.Tests` included)
- [x] 4.2 `dotnet format` whitespace check and slopwatch
- [x] 4.3 `openspec validate --all --no-interactive` and `bash scripts/check-openspec-root.sh`
- [ ] 4.4 Final commit carries the footer `Release-As: 0.5.0` (user-approved exception to the single-line rule; coordinate with ha-njord so both release together)
