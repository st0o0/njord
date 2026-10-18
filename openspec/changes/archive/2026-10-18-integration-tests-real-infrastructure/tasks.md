## 1. Project and host smoke

- [x] 1.1 Create `Njord.IntegrationTests` (MTP, shared props, references host and `Njord.Tests.Shared`), add it to `Njord.slnx`, and add `Microsoft.AspNetCore.Mvc.Testing`, `Grpc.Net.Client` and `Testcontainers` to `Directory.Packages.props` with `dotnet add package`
- [x] 1.2 Extract the composition of `Program.cs` into `NjordHost.Create` and add `NjordTestHost` (test server, temp SQLite directory and override file, fake Open-Meteo client, fixed config)
- [x] 1.3 Specs: `/healthz` and `/alive` endpoints, metrics endpoint exposing `njord_` meters, invalid configuration fails startup with the validation message

## 2. gRPC over a channel

- [x] 2.1 Specs for weather (unary and streaming), ops, admin (config read and update) and sensor services through a `GrpcChannel` on the test server
- [x] 2.2 Specs for the `Unavailable` and `DeadlineExceeded` mappings with a stopped actor and a slow actor

## 3. Persistence with restart

- [x] 3.1 Spec: budget usage recorded in the first host lifetime is reported by the second on the same SQLite file
- [x] 3.2 Spec: a learned poll cycle (steady phase) survives the restart
- [x] 3.3 Spec: an unusable database path fails startup validation with a clear message

## 4. MQTT against a broker

- [x] 4.1 Add the Mosquitto container fixture (pinned image, anonymous config, per-class lifetime, descriptive failure when Docker is missing)
- [x] 4.2 Specs: retained state payloads equal the golden masters, retained `online`/`offline` service status, last will on abrupt disconnect
- [x] 4.3 Spec: reconnect and republish of all retained topics after a broker restart

## 5. CI, docs, verification

- [x] 5.1 The project runs in about one minute (22 specs, Mosquitto container start included); no timeout change needed for the shared workflow
- [x] 5.2 Update `docs/development` (how to run, Docker requirement, namespace filters), `AGENTS.md` (build and test, test counts, project list) and `DisabledTestArchitectureSpec`
- [x] 5.3 Run `dotnet format whitespace`, build `Njord.slnx`, run every test project including the new one, `openspec validate --all --no-interactive`, `scripts/check-openspec-root.sh`
