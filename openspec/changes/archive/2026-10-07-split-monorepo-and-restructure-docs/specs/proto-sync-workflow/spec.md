## ADDED Requirements

### Requirement: njord release dispatches proto-sync event

When a njord service release is created, the release workflow SHALL dispatch a `proto-sync` `repository_dispatch` event to `st0o0/ha-njord` with the released version in the payload.

#### Scenario: Release triggers dispatch to ha-njord

- **WHEN** release-please creates a njord service release (tag `njord-v*`)
- **THEN** a `repository_dispatch` event of type `proto-sync` is sent to `st0o0/ha-njord` with `{"version": "<released version>"}`

### Requirement: ha-njord creates PR on proto-sync event

The `ha-njord` repo SHALL have a `sync-protos.yml` workflow that, on `repository_dispatch` of type `proto-sync`, checks out the njord protos, copies them into `protos/`, runs `make proto` to regenerate stubs, and creates a PR if files changed.

#### Scenario: Proto change creates PR

- **WHEN** a `proto-sync` event arrives and the protos have changed
- **THEN** a PR titled "chore: sync protos from njord v<version>" is created in ha-njord with updated proto source files and regenerated Python stubs

#### Scenario: No proto change creates no PR

- **WHEN** a `proto-sync` event arrives and no proto files have changed
- **THEN** no PR is created

### Requirement: Sync uses sparse checkout

The workflow SHALL use sparse checkout to fetch only the `protos/` directory from the njord repo, not the entire repo.

#### Scenario: Only protos directory is checked out

- **WHEN** the sync workflow checks out the njord repo
- **THEN** only the `protos/` directory is fetched
