# test-options-monitor Specification

## Purpose

A single shared `IOptionsMonitor<T>` test fake, so specs that need to simulate runtime config changes don't each hand-roll their own mutable monitor.
## Requirements
### Requirement: Shared TestOptionsMonitor in Njord.Tests.Shared
A generic `TestOptionsMonitor<T>` class SHALL exist in `Njord.Tests.Shared` that implements `IOptionsMonitor<T>`. It SHALL support an `Update(T newValue)` method that sets the current value and notifies all registered `OnChange` listeners.

#### Scenario: Initial value is accessible
- **WHEN** a `TestOptionsMonitor<T>` is constructed with an initial value
- **THEN** `CurrentValue` SHALL return that initial value

#### Scenario: Update notifies listeners
- **WHEN** `Update(newValue)` is called
- **THEN** all registered `OnChange` listeners SHALL be called with the new value

#### Scenario: Get with named options returns current value
- **WHEN** `Get(name)` is called
- **THEN** it SHALL return `CurrentValue` regardless of the name

### Requirement: Tests use the shared TestOptionsMonitor
Tests that need an `IOptionsMonitor<T>` SHALL use `TestOptionsMonitor<T>` from `Njord.Tests.Shared`; no test project SHALL define its own `MutableOptionsMonitor` or `FakeOptionsMonitor`.

#### Scenario: Budget provider spec uses the shared fake
- **WHEN** `OptionsBudgetProviderSpec` needs an `IOptionsMonitor<PollingOptions>`
- **THEN** it uses `TestOptionsMonitor<PollingOptions>` from `Njord.Tests.Shared`

