## 1. Create Njord.Mqtt.Tests

- [ ] 1.1 Create `src/Njord.Mqtt.Tests/Njord.Mqtt.Tests.csproj` — reference `Njord.Mqtt`, `Njord.Tests.Shared`, copy structure from `src/Njord.Egress.Tests/Njord.Egress.Tests.csproj`
- [ ] 1.2 Move 11 spec files from `src/Njord.Tests/Mqtt/` to `src/Njord.Mqtt.Tests/`: `ConsensusSnapshotSpec.cs`, `DiscoveryActorSpec.cs`, `DiscoveryPayloadBuilderSpec.cs`, `EnrichmentDiscoverySnapshotSpec.cs`, `EnrichmentGoldenMasterFixtures.cs`, `EnrichmentStateSnapshotSpec.cs`, `MqttConnectionActorSpec.cs`, `MqttEgressActorSpec.cs`, `StatePayloadBuilderSpec.cs`, `TopicSchemeSpec.cs`, `Presentation/HistoryPresenterSpec.cs`
- [ ] 1.3 Move 12 Verify `.verified.txt` snapshot files alongside their specs
- [ ] 1.4 Update namespaces from `Njord.Tests.Mqtt` → `Njord.Mqtt.Tests` (and `Njord.Tests.Mqtt.Presentation` → `Njord.Mqtt.Tests.Presentation`)
- [ ] 1.5 Add `Njord.Mqtt.Tests` to `src/Njord.slnx`
- [ ] 1.6 Check if `ModuleInitializer.cs` Verify settings are needed — if yes, create one in the new project

## 2. Create Njord.Enrichment.Tests

- [ ] 2.1 Create `src/Njord.Enrichment.Tests/Njord.Enrichment.Tests.csproj` — reference `Njord.Enrichment`, `Njord.Tests.Shared`
- [ ] 2.2 Move 10 spec files from `src/Njord.Tests/Enrichment/` to `src/Njord.Enrichment.Tests/`: `EnrichmentActorCollection.cs`, `EnrichmentActorSpec.cs`, `EnrichmentFeatureContractSpec.cs`, `ForecastHistoryActorSpec.cs`, `ForecastHistoryStateSpec.cs`, `Features/AlertEnrichmentSpec.cs`, `Features/DerivedEnrichmentSpec.cs`, `Features/HistoryEnrichmentSpec.cs`, `Features/IndexEnrichmentSpec.cs`, `Features/TrendEnrichmentSpec.cs`
- [ ] 2.3 Update namespaces from `Njord.Tests.Enrichment` → `Njord.Enrichment.Tests`
- [ ] 2.4 Add `Njord.Enrichment.Tests` to `src/Njord.slnx`

## 3. Create Njord.Ingest.Tests

- [ ] 3.1 Create `src/Njord.Ingest.Tests/Njord.Ingest.Tests.csproj` — reference `Njord.Ingest`, `Njord.Tests.Shared`
- [ ] 3.2 Move `src/Njord.Tests/Ingest/OpenMeteoClientSpec.cs` to `src/Njord.Ingest.Tests/`
- [ ] 3.3 Update namespace from `Njord.Tests.Ingest` → `Njord.Ingest.Tests`
- [ ] 3.4 Add `Njord.Ingest.Tests` to `src/Njord.slnx`

## 4. Create Njord.Sensors.Tests

- [ ] 4.1 Create `src/Njord.Sensors.Tests/Njord.Sensors.Tests.csproj` — reference `Njord.Sensors`, `Njord.Tests.Shared`
- [ ] 4.2 Move `src/Njord.Tests/Sensors/SensorHubActorSpec.cs` to `src/Njord.Sensors.Tests/`
- [ ] 4.3 Update namespace from `Njord.Tests.Sensors` → `Njord.Sensors.Tests`
- [ ] 4.4 Add `Njord.Sensors.Tests` to `src/Njord.slnx`

## 5. Clean up Njord.Tests

- [ ] 5.1 Remove empty `Mqtt/`, `Enrichment/`, `Ingest/`, `Sensors/` directories from `src/Njord.Tests/`
- [ ] 5.2 Verify `Njord.Tests` still contains: `Configuration/`, `Health/`, `Pipeline/`, `Persistence/`, `Actors/`, `ModuleInitializer.cs`
- [ ] 5.3 Remove `Njord.Mqtt` and `Njord.Enrichment` project references from `Njord.Tests.csproj` if they are no longer needed (check if remaining tests reference them — Persistence spec may need Enrichment)

## 6. Update documentation

- [ ] 6.1 Update AGENTS.md test project list and test counts
- [ ] 6.2 Update AGENTS.md note about "Mqtt and Enrichment specs stay in the host test project" — remove that note

## 7. Validation

- [ ] 7.1 Build entire solution: `dotnet build src/Njord.slnx`
- [ ] 7.2 Run ALL test suites (including new ones):
  ```
  for p in src/Njord.*Tests; do
    [ "$p" = src/Njord.Tests.Shared ] && continue
    dotnet run --project "$p/$(basename $p).csproj" --no-build
  done
  ```
- [ ] 7.3 Run architecture tests: `dotnet run --project src/Njord.Architecture.Tests/Njord.Architecture.Tests.csproj --no-build`
- [ ] 7.4 Run slopwatch: `dotnet tool restore && dotnet slopwatch analyze -d . --fail-on warning`
- [ ] 7.5 Run dotnet format: `dotnet format src/Njord.slnx --verify-no-changes`
