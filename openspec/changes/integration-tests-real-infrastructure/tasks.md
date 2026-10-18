## 1. Project and host smoke

- [ ] 1.1 Create `Njord.IntegrationTests` (MTP, shared props, references host and `Njord.Tests.Shared`), add it to `Njord.slnx`, and add `Microsoft.AspNetCore.Mvc.Testing`, `Grpc.Net.Client` and `Testcontainers` to `Directory.Packages.props` with `dotnet add package`
- [ ] 1.2 Add `public partial class Program;` to the host and an `NjordHostFactory` (temp SQLite directory, fake Open-Meteo client, fixed config, optional `FakeTimeProvider`)
- [ ] 1.3 Specs: readiness and liveness endpoints, metrics endpoint exposing `njord_` meters, invalid configuration fails startup with the validation message

## 2. gRPC over a channel

- [ ] 2.1 Specs for weather (unary and streaming), ops, admin (config read and update) and sensor services through a `GrpcChannel` on the test server
- [ ] 2.2 Specs for the `Unavailable` and `DeadlineExceeded` mappings with a stopped actor and a slow actor

## 3. Persistence with restart

- [ ] 3.1 Spec: budget usage recorded in the first host lifetime is reported by the second on the same SQLite file
- [ ] 3.2 Spec: a learned poll cycle (steady phase) survives the restart
- [ ] 3.3 Spec: an unusable database path fails startup validation with a clear message

## 4. MQTT against a broker

- [ ] 4.1 Add the Mosquitto container fixture (pinned image, anonymous config, per-class lifetime, descriptive failure when Docker is missing)
- [ ] 4.2 Specs: retained state payloads equal the golden masters, retained `online`/`offline` service status, last will on abrupt disconnect
- [ ] 4.3 Spec: reconnect and republish of all retained topics after a broker restart

## 5. CI, docs, verification

- [ ] 5.1 Measure the added CI time and document it; note a required timeout change for the shared workflow if needed
- [ ] 5.2 Update `docs/development` (how to run, Docker requirement, namespace filters), `AGENTS.md` (build and test, test counts, project list) and `DisabledTestArchitectureSpec`
- [ ] 5.3 Run `dotnet format whitespace`, build `Njord.slnx`, run every test project including the new one, `openspec validate --all --no-interactive`, `scripts/check-openspec-root.sh`
