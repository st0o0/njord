## MODIFIED Requirements

### Requirement: No wall-clock TimeProvider in tests
All test code SHALL use `Microsoft.Extensions.Time.Testing.FakeTimeProvider` with a fixed seed timestamp instead of `TimeProvider.System`. The standard fixed epoch SHALL be `new DateTimeOffset(2026, 7, 12, 6, 0, 0, TimeSpan.Zero)`.

#### Scenario: Token bucket refill test uses FakeTimeProvider
- **WHEN** a test needs to verify time-based token refill in `GatewayBudgetGate`
- **THEN** it SHALL use `FakeTimeProvider.Advance()` instead of `Thread.Sleep`

#### Scenario: No custom FakeTimeProvider shims
- **WHEN** a test class needs a controllable time source
- **THEN** it SHALL use `Microsoft.Extensions.Time.Testing.FakeTimeProvider`, not a locally-defined shim class
