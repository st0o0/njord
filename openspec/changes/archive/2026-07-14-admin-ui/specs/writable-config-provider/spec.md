# writable-config-provider Specification

## Purpose

An in-memory configuration overlay that sits last in the .NET configuration provider chain, supports runtime mutation with `IOptionsMonitor<T>` change notification, and optionally flushes overrides to a JSON file for restart persistence.

## ADDED Requirements

### Requirement: WritableConfigProvider is a MemoryConfigurationProvider subclass
The system SHALL provide a `WritableMemoryConfigurationProvider` that extends `MemoryConfigurationProvider` and exposes an `Update(string key, string? value)` method that calls `Set(key, value)` followed by `OnReload()` to fire the `IChangeToken`.

#### Scenario: Single value update triggers IOptionsMonitor
- **WHEN** `Update("Njord:PollInterval", "00:30:00")` is called
- **THEN** `IOptionsMonitor<NjordOptions>` fires its `OnChange` callback with the new poll interval

#### Scenario: Multiple values updated atomically
- **WHEN** `UpdateBatch(IDictionary<string, string?> values)` is called with 3 key-value pairs
- **THEN** all 3 values are set before a single `OnReload()` fires, so consumers see a consistent snapshot

### Requirement: WritableConfigProvider is registered last in the provider chain
The `WritableMemoryConfigurationSource` SHALL be added after all other configuration sources (appsettings.json, environment variables) so its values take precedence. It SHALL be registered in `Program.cs` or `NjordServiceSetup` via `builder.Configuration.Add(source)`.

#### Scenario: Override wins over appsettings.json
- **WHEN** `appsettings.json` sets `Njord:PollInterval` to `01:00:00` and the writable provider sets it to `00:30:00`
- **THEN** `IOptionsMonitor<NjordOptions>.CurrentValue.PollInterval` is 30 minutes

#### Scenario: Override wins over environment variable
- **WHEN** env var `Njord__PollInterval` is `01:00:00` and the writable provider sets it to `00:30:00`
- **THEN** `IOptionsMonitor<NjordOptions>.CurrentValue.PollInterval` is 30 minutes

### Requirement: WritableConfigProvider is resolvable from DI
The `WritableMemoryConfigurationProvider` instance SHALL be registered as a singleton in DI so that API controllers and services can inject it directly. It SHALL NOT require casting `IConfigurationRoot` and searching providers.

#### Scenario: Controller injects provider
- **WHEN** an API controller constructor declares `WritableMemoryConfigurationProvider provider`
- **THEN** the DI container resolves the same instance that is in the configuration chain

### Requirement: WritableConfigProvider flushes overrides to a JSON file
The provider SHALL periodically flush its current override values to a JSON file at a configurable path (default `/app/data/config-overrides.json`). The flush SHALL be debounced (no more than once per 5 seconds after the last change). On startup, the provider SHALL load existing overrides from this file if it exists.

#### Scenario: Override persisted to file
- **WHEN** a value is updated and 5 seconds elapse with no further changes
- **THEN** the override file contains the updated key-value pair as nested JSON matching the configuration path structure

#### Scenario: Overrides loaded on startup
- **WHEN** the service starts and `/app/data/config-overrides.json` exists with `{"Njord":{"PollInterval":"00:30:00"}}`
- **THEN** the writable provider contains `Njord:PollInterval = 00:30:00` before any API call

#### Scenario: Missing override file on startup is not an error
- **WHEN** the service starts and no override file exists
- **THEN** the writable provider starts empty and the service uses values from lower-priority providers

### Requirement: WritableConfigProvider supports removing overrides
The provider SHALL support removing individual overrides (reverting to the underlying provider's value) via `Remove(string key)`. Removing a key SHALL call `OnReload()` and update the flush file.

#### Scenario: Removing an override reverts to base value
- **WHEN** `appsettings.json` sets `Njord:PollInterval` to `01:00:00`, the override sets it to `00:30:00`, then `Remove("Njord:PollInterval")` is called
- **THEN** `IOptionsMonitor<NjordOptions>.CurrentValue.PollInterval` reverts to 60 minutes
