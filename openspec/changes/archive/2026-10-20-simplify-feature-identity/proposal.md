## Why

Three enums (`FeatureKind` 35, `AlertType` 19, `LifestyleKind` 16) plus `FeatureKindConfigMappings` encode the same set of features, and every new feature touches all of them. Naming drifts between them (`BBQScore`/`BbqScore`, `ApproachingFront`/`FrontApproaching`). `OutlookConfidence` duplicates `ConfidenceLevel`.

## What Changes

- **BREAKING** Feature identity is `OutputKind` (Alert, Score, Condition, Trend, Forecast) plus a `Name` string on the handler/rule. Config key is `Category:Name`, wire key is the lowercase `Name`.
- **BREAKING** Config is hierarchical: `Njord:Features:{Category}:{Name}` (for example `Njord__Features__Weather__Frost=false`). Category `WeatherAlerts` becomes `Weather`.
- `FeatureDescriptor` shrinks to `Output`, `Category`, `DefaultEnabled`, `Requirements`; descriptions live only in `docs/data/features.json`.
- Handlers return one `FeatureResult` hierarchy (`AlertResult`, `ScoreResult`, `ConditionResult`, `TrendResult`, `ForecastResult`) instead of ~20 event types.
- **BREAKING** gRPC: `AlertType` enum removed, `Alert.key` is a string; `LifestyleUpdate.type` is the lowercase feature name and carries `OutputKind`.
- Delete `FeatureKind`, `AlertType`, `LifestyleKind`, `LifestyleMappings`, `FeatureKindConfigMappings`, `OutlookConfidence`, `UvLevel`, `ExposureRisk`, `HumidityComfort` (alert details carry strings).
- Remove unused `TimeResolution` members and an unused `DomainKind.Solar` if nothing fetches it.

## Non-goals

- No change to polling, domains fetched or alert thresholds. API budget impact: none.
- No migration of old config keys.

## Impact

`Njord.Messages`, `Njord.Core`, all domain projects, `Njord.Grpc`, `protos/`, Config Builder (`docs/.vitepress/theme/builder`), `docs/data/features.json`, compose examples, e2e files.
