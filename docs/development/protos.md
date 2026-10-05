# Proto Management

The gRPC API is defined by protobuf files shared between the njord service (.NET) and the ha-njord integration (Python).

## Source of truth

Proto definitions live in the **njord** repo at `protos/njord/v2/`:

```
protos/njord/v2/
├── common.proto      # Shared types (forecasts, enrichments, alerts, ...)
├── weather.proto     # WeatherService (GetForecast, StreamForecasts, ...)
├── admin.proto       # AdminService (GetConfig, StreamConfig, ...)
├── ops.proto         # OpsService (TriggerPoll, GetStatus, ...)
└── sensor.proto      # SensorService (Push, StreamPush)
```

## How each repo uses them

| Repo | Language | Stub generation |
|------|----------|-----------------|
| njord | C# | `Grpc.Tools` MSBuild integration — stubs are generated at build time from `protos/` |
| ha-njord | Python | `make proto` — runs `grpc_tools.protoc` via Docker, writes stubs to `custom_components/njord/proto/` |

## Sync workflow

When protos change in the njord repo:

1. Edit the `.proto` files in `njord/protos/njord/v2/`
2. The .NET service picks up changes at build time automatically
3. Copy the updated protos to `ha-njord/protos/` and run `make proto` to regenerate Python stubs
4. Commit both repos

Proto changes are rare after the API stabilizes. The protos follow an extend-only contract: new fields and services are added, existing field numbers are never reused.

## Automated sync (optional)

When a njord release is created, the release workflow dispatches a `proto-sync` event to ha-njord via GitHub Actions `repository_dispatch`. This triggers a workflow in ha-njord that:

1. Sparse-checkouts `protos/` from the njord repo
2. Copies them into `ha-njord/protos/`
3. Runs `make proto` to regenerate Python stubs
4. Creates a PR if anything changed

### Setup

The njord repo needs a `HA_NJORD_PAT` secret — a GitHub Personal Access Token with `repo` scope for `st0o0/ha-njord`. Create the token under **Settings > Developer settings > Personal access tokens** and add it as a repository secret in the njord repo under **Settings > Secrets and variables > Actions**.
