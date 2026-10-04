## ADDED Requirements

### Requirement: NjordFixture boots the real HTTP/gRPC pipeline with stubbed actors
The integration test fixture SHALL build the ASP.NET host using `WebApplicationBuilder` with the real middleware pipeline (health endpoints, gRPC services, Kestrel configuration) but with every actor key resolved to a `TestProbe` instead of a real actor.

#### Scenario: Fixture provides HttpClient for health endpoint testing
- **WHEN** a test obtains an `HttpClient` from the fixture
- **THEN** requests to `/alive` and `/healthz` SHALL be served by the real middleware pipeline

#### Scenario: Fixture provides gRPC channel for service testing
- **WHEN** a test obtains a gRPC channel from the fixture
- **THEN** the channel SHALL connect to the in-process `TestServer` and reach the real gRPC service implementations

#### Scenario: All actor keys resolve to TestProbes
- **WHEN** the fixture initializes the actor system
- **THEN** every actor key in `ActorKeys.cs` (ISchedulerActor, IBudgetTrackerActor, IPipelineActor, IModelStateActor, IEnrichmentActor, ISensorHubActor, IForecastSnapshotActor, IEnrichmentSnapshotActor, IGrpcSnapshotConsumerActor, IMqttConnectionActor, IMqttStateActor, IMqttDiscoveryActor) SHALL be registered with a `TestProbe`

### Requirement: Tests are grouped into domain collections sharing the fixture
Tests using the fixture SHALL be organized into xUnit collections that share a single `NjordFixture` instance via `ICollectionFixture<NjordFixture>`.

#### Scenario: Health collection tests share the fixture
- **WHEN** multiple health endpoint tests run
- **THEN** they SHALL share the same fixture instance and run sequentially within the collection

#### Scenario: gRPC collections run independently
- **WHEN** Weather, Ops, Admin, and Sensor gRPC test collections run
- **THEN** each collection SHALL share its own fixture instance and collections MAY run in parallel with each other

### Requirement: Fixture does not use SQLite persistence
The fixture SHALL configure in-memory journal and snapshot store to avoid file contention between test runs.

#### Scenario: No persistence files created during integration tests
- **WHEN** integration tests run
- **THEN** no SQLite database files SHALL be created on disk
