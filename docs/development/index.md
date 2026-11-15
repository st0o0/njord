# Contributing

njord is a .NET 10 service built on Akka.NET. This section covers the internals for contributors.

## Conventions

- **Language:** all code, specs, docs and commits in English
- **Commits:** [Conventional Commits](https://www.conventionalcommits.org/) (commitlint-enforced), single-line message
- **Git:** never `git push` - the maintainer pushes. Never edit `<Version>` in `Directory.Build.props` (release-please)
- **C#:** `sealed` by default, records for messages/DTOs, nullable enabled, no XML docs
- **Akka:** setups via `ISetup<T>`, actors resolved by marker, messages in `Njord.Messages`

See `AGENTS.md` in the repository root for the full convention reference.

## Workflow

Changes go through OpenSpec: explore, propose (design + specs + tasks), implement (TDD), archive. The `openspec/` directory in the repository root holds all change artifacts.

## Pages

- [Architecture](./architecture) - system design and diagrams
- [Repository layout](./layout) - solution structure and project responsibilities
- [Building & testing](./building) - build, test, format commands
- [Actors](./actors) - actor topology and addressing
- [Data flow](./data-flow) - polling, pipeline, rule execution
- [gRPC API](./grpc) - the 4 RPCs and entity-first design
- [Local development](./local-dev) - Docker stack, debugging, mutation testing
