## 1. Production code — NjordActorSystemSetup

- [x] 1.1 In `src/Njord/Configuration/NjordActorSystemSetup.cs` `ConfigureSystem` (lines 61-62): change `.WithRemoting(new RemoteOptions { HostName = "localhost", Port = 0 })` to `Port = 2552`, and change `.WithClustering()` to `.WithClustering(new ClusterOptions { SeedNodes = ["akka.tcp://njord@localhost:2552"] })`
- [x] 1.2 In `WithNjordActors` (lines 132-139): remove the `WithActors` callback containing `cluster.Join(selfAddress)`. Replace it with a `WithActors` callback that only calls `AddStreamShutdownTask(system, registry, StreamStopTimeout)`
- [x] 1.3 Build `dotnet build src/Njord.slnx` — verify 0 errors

## 2. Integration tests — NjordFixture

- [x] 2.1 In `src/Njord.IntegrationTests/Infrastructure/NjordFixture.cs` (lines 128-133): change `.WithRemoting` to `Port = 0` (keep ephemeral for tests) and `.WithClustering()` to `.WithClustering(new ClusterOptions { SeedNodes = ["akka.tcp://njord@localhost:0"] })` — or remove the manual `cluster.Join` and use a fixed test port. Evaluate which approach is cleanest for parallel test execution
- [x] 2.2 Remove the `cluster.Join(cluster.SelfAddress)` line from the `WithActors` callback in NjordFixture — keep the probe registrations and `System = system` assignment in that callback

## 3. Integration tests — ActorKeyRegistrationSpec

- [x] 3.1 In `src/Njord.IntegrationTests/Configuration/ActorKeyRegistrationSpec.cs` (lines 35-36): align `WithRemoting`/`WithClustering` with the new pattern. Since `WithNjordActors` no longer does `cluster.Join`, this spec needs its own cluster formation (SeedNodes or inline join before the `WithNjordActors` call)

## 4. Validation

- [x] 4.1 Run `dotnet run --project src/Njord.IntegrationTests/Njord.IntegrationTests.csproj -- -class "Njord.IntegrationTests.Configuration.ActorKeyRegistrationSpec"` — all 12 tests pass
- [x] 4.2 Run full solution build: `dotnet build src/Njord.slnx` — 0 errors
- [x] 4.3 Run `dotnet run --project src/Njord.IntegrationTests/Njord.IntegrationTests.csproj` — all integration tests pass
- [x] 4.4 E2E smoke test: `docker compose -f e2e/docker-compose.e2e.yml up -d --build`, check njord logs for absence of "ClusterSingletonProxy failed" warnings within first 60 seconds, then `docker compose -f e2e/docker-compose.e2e.yml down -v`
