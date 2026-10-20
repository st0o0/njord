## Why

`AirQualityAlertHandler` is registered three times as `IDomainHandler` with magic string names (`"Pollen"`, `"Pm25"`, `"Dust"`). Each instance receives all AQ alert rules and filters to its own by name. The Weather side already has the clean pattern: a single `WeatherAlertHandler : IMultiFeatureHandler` that takes all rules, lazy-resolves the enabled subset via `FeatureOptions`, and exposes a composite `Feature`. The AQ side should follow the same pattern.

## What Changes

- `AirQualityAlertHandler` implements `IMultiFeatureHandler` instead of `IDomainHandler`. It takes all `IAlertRule<DomainForecast<AirQualityPoint>>` rules and `IOptions<FeatureOptions>`, lazy-resolves enabled rules (same `Enabled` record pattern as `WeatherAlertHandler`), and its `Name` becomes `"AirQualityAlerts"`.
- The three factory-lambda registrations in `ServiceCollectionExtensions.AddAlertHandlers()` collapse to a single `AddSingleton<IDomainHandler, AirQualityAlertHandler>()`.
- Tests update to use the new single-instance pattern and verify per-feature toggling.

## Capabilities

### New Capabilities

None. Behavior is preserved.

### Modified Capabilities

None. No spec-level requirement changes; this is a pure internal refactor.

## Non-goals

- No new alert rules or features.
- No changes to `WeatherAlertHandler`, flood handlers, or marine handlers.
- No changes to `FeatureResolution`, `IMultiFeatureHandler`, or `IAlertRule<T>`.
- No wire, gRPC, or configuration changes.

## API-budget impact

None. No polling or request changes.

## Impact

- `src/Njord.Alerts/AirQualityAlertHandler.cs` — refactored to `IMultiFeatureHandler`.
- `src/Njord.Alerts/ServiceCollectionExtensions.cs` — registration simplified.
- `src/Njord.Alerts.Tests/AirQualityAlertHandlerSpec.cs` — tests updated.
- `src/Njord.Tests.Shared/FeatureServices.cs` — optional helper added.
