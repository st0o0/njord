## ADDED Requirements

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

### Requirement: Duplicated MutableOptionsMonitor fakes are replaced
All duplicated `MutableOptionsMonitor` or `FakeOptionsMonitor` implementations in test files SHALL be replaced with `TestOptionsMonitor<T>` from `Njord.Tests.Shared`.

#### Scenario: OpsGrpcServiceSpec uses shared fake
- **WHEN** `OpsGrpcServiceSpec` needs an `IOptionsMonitor<NjordOptions>`
- **THEN** it SHALL use `TestOptionsMonitor<NjordOptions>` from `Njord.Tests.Shared`
