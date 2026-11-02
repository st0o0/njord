## MODIFIED Requirements

### Requirement: IActorRegistration interface for domain actor declarations
The system SHALL define an `IActorRegistration` interface with a method that accepts an `AkkaConfigurationBuilder` and `IServiceProvider`. Domain setups SHALL register `IActorRegistration` implementations in DI during their `Setup` call. The central `AkkaSetup` SHALL resolve all `IActorRegistration` instances from the service provider and invoke them during `BuildSystem`.

#### Scenario: Domain setup registers its actors via IActorRegistration
- **WHEN** a domain setup's `Setup` is called
- **THEN** it SHALL register an `IActorRegistration` implementation that configures its actors on `AkkaConfigurationBuilder`

#### Scenario: AkkaSetup resolves and executes all registrations
- **WHEN** `AkkaSetup.BuildSystem` is called
- **THEN** it SHALL resolve all `IActorRegistration` instances from `IServiceProvider` and invoke each one on the builder

#### Scenario: Registration order is deterministic
- **WHEN** multiple `IActorRegistration` implementations are registered
- **THEN** they SHALL be invoked in the order they were registered in DI (matching the setup chain order in `Program.cs`)

## REMOVED Requirements

### Requirement: NjordHost.Configure is a pure composition chain
**Reason**: The setup chain is inlined directly into `Program.cs` and `NjordTestHost`. The shared `NjordHost` class is unnecessary indirection; `HostSmokeTests` validates the full composition against real endpoints, catching any divergence.
**Migration**: Move the setup calls from `NjordHost.Configure` into `Program.cs` and `NjordTestHost.StartAsync`. Delete `NjordHost.cs`.
