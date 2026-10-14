## ADDED Requirements

### Requirement: Tests use the shared TestOptionsMonitor
Tests that need an `IOptionsMonitor<T>` SHALL use `TestOptionsMonitor<T>` from `Njord.Tests.Shared`; no test project SHALL define its own `MutableOptionsMonitor` or `FakeOptionsMonitor`.

#### Scenario: Budget provider spec uses the shared fake
- **WHEN** `OptionsBudgetProviderSpec` needs an `IOptionsMonitor<PollingOptions>`
- **THEN** it uses `TestOptionsMonitor<PollingOptions>` from `Njord.Tests.Shared`

## REMOVED Requirements

### Requirement: Duplicated MutableOptionsMonitor fakes are replaced
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Tests use the shared TestOptionsMonitor" in this capability.
