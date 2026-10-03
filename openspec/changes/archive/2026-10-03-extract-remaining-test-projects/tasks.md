## 1. Create Njord.Mqtt.Tests

- [x] 1.1 Created csproj referencing Njord.Mqtt, Njord.Enrichment, Njord.Egress, Njord.Tests.Shared + Verify.XunitV3
- [x] 1.2 Moved 11 spec files + 14 .verified.txt golden masters
- [x] 1.3 Moved Presentation/HistoryPresenterSpec.cs
- [x] 1.4 Updated namespaces, created ModuleInitializer.cs and local FailingRefProvider.cs
- [x] 1.5 Added to Njord.slnx
- [x] 1.6 ModuleInitializer with Verify DiffRunner.Disabled created

## 2. Create Njord.Enrichment.Tests

- [x] 2.1 Created csproj referencing Njord.Enrichment, Njord.Egress, Njord.Mqtt, Njord.Pipeline, Njord.Tests.Shared
- [x] 2.2 Moved 10 spec files including Features/ subfolder
- [x] 2.3 Updated namespaces
- [x] 2.4 Added to Njord.slnx

## 3. Create Njord.Ingest.Tests

- [x] 3.1 Created csproj referencing Njord.Ingest, Njord.Tests.Shared (no TestKit)
- [x] 3.2 Moved OpenMeteoClientSpec.cs
- [x] 3.3 Updated namespace
- [x] 3.4 Added to Njord.slnx

## 4. Create Njord.Sensors.Tests

- [x] 4.1 Created csproj referencing Njord.Sensors, Njord.Tests.Shared
- [x] 4.2 Moved SensorHubActorSpec.cs
- [x] 4.3 Updated namespace
- [x] 4.4 Added to Njord.slnx

## 5. Clean up Njord.Tests

- [x] 5.1 Removed empty Mqtt/, Enrichment/, Ingest/, Sensors/ directories
- [x] 5.2 Njord.Tests retains: Configuration, Health, Pipeline, Persistence, Actors, ModuleInitializer
- [x] 5.3 Verify.XunitV3 kept (Persistence spec uses it)

## 6. Update documentation

- [x] 6.1 Updated AGENTS.md test project list and test counts
- [x] 6.2 Removed note about host-resident Mqtt/Enrichment specs

## 7. Validation

- [x] 7.1 Build: 26 projects, 0 errors, 0 warnings
- [x] 7.2 All test suites pass (total 859 across all projects)
- [x] 7.3 Architecture tests: 27/27 pass
- [x] 7.4 Slopwatch: 0 issues
- [x] 7.5 dotnet format: clean
