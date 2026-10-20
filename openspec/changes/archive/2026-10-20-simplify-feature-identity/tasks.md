## 1. Identity and config

- [x] 1.1 Add `OutputKind`, `IFeature` (Name + descriptor), slim `FeatureDescriptor` in `src/Njord.Core/Domain`
- [x] 1.2 Hierarchical `FeatureOptions` and validator in `src/Njord.Core/Configuration`

## 2. Results and handlers

- [x] 2.1 `FeatureResult` hierarchy in `src/Njord.Messages/Pipeline`, drop old `PipelineEvent` result types
- [x] 2.2 Update all handlers and rules in Weather, AirQuality, Flood, Marine

## 3. gRPC

- [x] 3.1 Proto: remove `AlertType`, add `OutputKind`, string alert key
- [x] 3.2 Mappings and consumer state in `src/Njord.Grpc`

## 4. Cleanup

- [x] 4.1 Delete obsolete enums, merge `OutlookConfidence`, detail enums become strings
- [x] 4.2 Remove unused `TimeResolution` members and `DomainKind.Solar`

## 5. Config Builder and docs

- [x] 5.1 Update `docs/.vitepress/theme/builder`, `docs/data/features.json`, compose examples, e2e, AGENTS.md, skill

## 6. Tests and validation

- [x] 6.1 Update all test projects
- [x] 6.2 `dotnet build Njord.slnx`, `dotnet format Njord.slnx`, run every test project with `dotnet run --project`
