## Context

`split-test-projects` is archived and shipped, but it did not update the two main specs that name `Njord.Tests`. Verified against the code: `src/Njord.slnx` lists `Njord.{Architecture,Core,Domain,Egress,Grpc,Persistence,Pipeline}.Tests`, `Njord.Tests` and `Njord.Tests.Shared`; `src/Njord.Architecture.Tests` references all other test projects (glob `Njord.*.Tests`), `NjordArchitecture.TestAssemblies` loads every `Njord.*Tests` assembly plus `Njord.Tests.Shared`, and the sealed/`Spec` and disabled-test specs run over those assemblies. No csproj references Testcontainers, WireMock or MQTTnet.

## Decisions

1. Rename "Unit and actor tests in Njord.Tests" with a RENAMED delta plus MODIFIED under the new name, so the requirement identity follows its new meaning. Scenarios are kept verbatim except the "run without Docker" command, which is generalised to a per-project command.
2. Restate the three architecture requirements in full with their scenarios; add one scenario ("New test project is covered automatically") that is true through the wildcard project reference and assembly scan.
3. AGENTS.md, CLAUDE.md and the njord skills were already updated by `split-test-projects`; only a verification pass is done.
4. The "no Testcontainers/WireMock/MQTTnet" constraint now applies to every test project as a direct package-reference rule (true today: no test csproj references them; MQTTnet reaches `Njord.Tests` only transitively through the host).

## Risks / Trade-offs

- Archive aborts if a MODIFIED block drops a base scenario: all base scenarios are restated. Fallback: edit the main spec by hand and archive with `--skip-specs`.
