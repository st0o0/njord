## Why

After the forecast-ensemble-config-split, several config-area inconsistencies remain: one validator class implements 6 `IValidateOptions<>` interfaces (hard to navigate), a PostConfigure bridge copies values between two options classes for SolarYield, FloodOptions is the only API-domain option living in Core instead of OpenMeteo, and `FeatureDescriptor.DefaultEnabled` is dead metadata that 66 features set but nothing in production reads.

## What Changes

- Split `OpenMeteoOptionsValidator` (6 interfaces) into one validator per options class, each living next to its options.
- Resolve the SolarYield PostConfigure bridge: `SolarYield` feature reads `IOptions<SolarYieldOptions>` directly for PeakKw/SystemEfficiency; remove dead defaults from `SolarYieldConfig`; remove the PostConfigure bridge from `DomainSetupContainer`.
- Move `FloodOptions` from `Njord.Core/Configuration/` to `Njord.OpenMeteo/Configuration/` alongside the other API-domain options.
- Remove `DefaultEnabled` from `FeatureDescriptor` and all 66 feature declarations. The config property initializer is the single source of truth for the default enabled state.

### Budget impact

No change to polling, domains, or API call patterns. Zero budget impact.

## Non-goals

- Moving `PollingOptions` or `HealthOptions` out of Core (they are core-level config, not API-specific).
- Changing validation rules or thresholds.
- Hot-reload or IOptionsMonitor.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `feature-rule-abstraction`: Remove `DefaultEnabled` from the `FeatureDescriptor` requirement.
