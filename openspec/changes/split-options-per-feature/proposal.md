## Why

Configuration is one god object with unclear runtime semantics. `NjordOptions` is
injected into a dozen classes that each need one or two values. `Mqtt` and `Enrichment`
are bound twice (nested in `NjordOptions` and standalone), and the enrichment validators
validate the parent while features consume the children. Hot reload is undefined: the admin
API writes config at runtime, `OpenMeteoGateway` and `GatewayBudgetGate` follow it through
`IOptionsMonitor`, but `PollScheduler`, `OpenMeteoClient`, `MqttStatePublisher` and every
analyzer captured `IOptions<T>.Value` at startup. After an admin change, part of the
system runs on new config and part on old. This is change 2 of 4 (after
`typed-analysis-models`, before `restructure-compute` and `separate-messages-from-api`).

## What Changes

- One options type per configuration section, bound once with `BindConfiguration` +
  `ValidateOnStart`. Its validator sits next to it, and the owning feature's setup
  extension registers both. Existing JSON paths and env var names stay as they are.
- `NjordOptions` shrinks to the forecast scope (locations, models, horizons, forecast
  days, parameters). Poll interval and budget override move to `PollingOptions`, which
  binds the same `Njord` section. `Mqtt` and `Enrichment` stop being nested properties.
- **BREAKING (internal)**: the dual binding of `EnrichmentOptions`/`MqttOptions`
  (parent + child) is removed. The parent `EnrichmentOptions` aggregate disappears.
- Explicit hot-reload classification:
  - **Hot**: analysis parameters (alert thresholds, index preferences, consensus, trend
    and derived parameters), poll interval, budget.
  - **Restart required**: locations, models, horizons, forecast days, parameters, feature
    `Enabled` toggles, MQTT, gRPC, persistence, sensors.
- A `ConfigManager` actor is the only `IOptionsMonitor.OnChange` subscriber and the only
  writer. It validates a candidate update with the section's validator before persisting,
  and sends typed change messages to the actors that consume hot settings. Actors never
  take `IOptionsMonitor<T>`.
- Compute never sees options. Each feature converts validated options into immutable
  settings records (`AlertSettings` with `SeverityThresholds`, `IndexSettings` with
  resolved preferences, …). `ForecastPipeline` receives settings changes through a side
  channel, like sensor data today, and passes the current settings to every computation.
- `IWritableOptions<NjordOptions>` becomes section-scoped writing into the same override
  file format (`data/appsettings.Override.json`, `Njord`-wrapped).
- Admin writes to restart-required sections are persisted and answered with
  `applied = true` plus a "takes effect after restart" warning, instead of being silently
  half-applied.

## Capabilities

### New Capabilities
- `options-hot-reload`: which settings apply at runtime, how changes reach actors, and how
  restart-required changes are reported.

### Modified Capabilities
- `standalone-sub-options`: every section is standalone. The nested/dual-bound exception
  for `Mqtt` and `Enrichment` is removed. Validators sit next to their options without
  exception.
- `enrichment-sub-options-binding`: sub-options are bound once. The coexisting parent
  binding is removed.
- `writable-options`: updates are section-scoped, validated before persisting, and
  propagated through the `ConfigManager`.
- `compute-library`: options and validators no longer live in or reference Compute.
  Compute receives settings records.
- `analyzer-di-cleanup`: analyzers receive current settings per computation. Only the
  `Enabled` toggle comes from `IOptions<T>` at startup.

## Non-goals

- No change to configuration keys, file locations or env var names.
- No change to the admin gRPC proto. The restart notice uses the existing `warnings` field.
- No new hot-reloadable settings beyond the classification above, and no live
  re-subscription of MQTT/gRPC endpoints.
- Routing the admin gRPC service through `ConfigManager` messages is part of
  `separate-messages-from-api`. In this change the service calls the section writer, which
  goes through the same validation path.
- Polling behavior is unchanged except that a changed poll interval now actually reaches
  the scheduler. API budget impact: none by default. A shorter interval set via admin is
  still validated against 80 % of the monthly budget (300k free tier) before it is applied.

## Impact

- `src/Njord.Core/Configuration/*` (options, validators, writer), `src/Njord/Configuration/*`
  (setup containers, new `ConfigManager`), every setup container in feature libraries,
  `src/Njord.Pipeline` (`PollScheduler`, `ForecastPipeline` settings side channel),
  `src/Njord.OpenMeteo` (`OpenMeteoClient`, `OpenMeteoGateway`, `GatewayBudgetGate`),
  `src/Njord.Mqtt`, `src/Njord.Grpc` (`AdminGrpcService`, `OpsGrpcService` reads),
  `src/Njord.Alerts|Derived|Trends|Indices`, `src/Njord/Health/PipelineHealthCheck.cs`.
- `src/Njord.Compute` drops its `Configuration/` folder in favor of settings records.
- Tests across `Njord.Core.Tests`, `Njord.Pipeline.Tests`, `Njord.OpenMeteo.Tests`,
  `Njord.Grpc.Tests`, `Njord.Tests`, and the feature test projects.
