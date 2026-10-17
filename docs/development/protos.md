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
| njord | C# | `Grpc.Tools` MSBuild integration, stubs are generated at build time from `protos/` |
| ha-njord | Python | `make proto` runs `grpc_tools.protoc` via Docker, writes stubs to `custom_components/njord/proto/` |

## Sync workflow

When protos change in the njord repo:

1. Edit the `.proto` files in `njord/protos/njord/v2/`
2. The .NET service picks up changes at build time automatically
3. Copy the updated protos to `ha-njord/protos/` and run `make proto` to regenerate Python stubs
4. Commit both repos

Proto changes are rare after the API stabilizes. The protos follow an extend-only contract: new fields and services are added, existing field numbers are never reused.

## Closed sets are enums

Closed value sets are proto enums, each with a `*_UNSPECIFIED = 0` member. Absent optional values mean "not delivered".

| Field | Enum | Members |
|-------|------|---------|
| `ModelStatus.phase`, `TriggerTarget.phase` | `PollPhase` | `DISCOVERY`, `STEADY` |
| `ParameterTrend.direction` | `TrendDirection` | `RISING`, `FALLING`, `STABLE` |
| `TrendUpdate.stability_label` | `Stability` | `CONVERGING`, `DIVERGING`, `STABLE` |
| `HorizonDerived.dewpoint_comfort` | `DewPointComfort` | `DRY`, `COMFORTABLE`, `STICKY`, `OPPRESSIVE`, `DANGEROUS` |
| `VpdInfo.category` | `VpdCategory` | `LOW`, `OPTIMAL`, `HIGH`, `CRITICAL` |
| `ConsensusConfig.method` | `ConsensusMethod` | `MEAN`, `MEDIAN`, `TRIMMED_MEAN` |

Member names carry the enum name as prefix (`POLL_PHASE_STEADY`). In `SetEnrichment`, an unspecified or absent `method` leaves the method unchanged and an undefined number is answered with a rejected `ConfigResponse`.

Clients must treat unknown enum numbers as `UNSPECIFIED`: protobuf delivers a member added later as its raw number, so adding members is not a breaking change. When these fields moved from `string` to enums, the old field numbers were `reserved` and the enums got new numbers.

## Configuration mutation results

All `AdminService` mutation RPCs (`SetLocations`, `SetSettings`, `SetEnrichment`, `SetBudget`) answer with a `ConfigResponse`:

| Field | Meaning |
|-------|---------|
| `applied` | The change was validated and persisted. |
| `rejection_reason` | The failure messages, joined with `; ` (empty when nothing failed). |
| `issues` | `SetEnrichment` only: one `ConfigIssue` per failure, with the `EnrichmentSection` it belongs to (`ENRICHMENT_SECTION_UNSPECIFIED` for the request as a whole). |
| `restart_required` | Settings that were changed but take effect only after a restart, for example `Enrichment.Trends.Enabled`. They no longer appear in `warnings`. |
| `warnings` | Budget projection warnings only. |
| `dry_run` | Echoes `SetEnrichmentRequest.dry_run`. |

`SetEnrichment` validates all touched sections first and persists them in one write, so either every section is applied or none. `reset` lists sections whose override is removed, so the base configuration (appsettings, environment variables) applies again; resets are applied before the updates of the same request, and `ENRICHMENT_SECTION_UNSPECIFIED` in `reset` is an issue. With `dry_run = true` the request is validated and answered without persisting: `applied` is `false`, and the request is valid exactly when `issues` is empty. A request with no section and no `reset` is rejected with one issue.

## Automated sync (optional)

When a njord release is created, the release workflow dispatches a `proto-sync` event to ha-njord via GitHub Actions `repository_dispatch`. This triggers a workflow in ha-njord that:

1. Sparse-checkouts `protos/` from the njord repo
2. Copies them into `ha-njord/protos/`
3. Runs `make proto` to regenerate Python stubs
4. Creates a PR if anything changed

### Setup

The njord repo needs a `HA_NJORD_PAT` secret: a GitHub Personal Access Token with `repo` scope for `st0o0/ha-njord`. Create the token under **Settings > Developer settings > Personal access tokens** and add it as a repository secret in the njord repo under **Settings > Secrets and variables > Actions**.
