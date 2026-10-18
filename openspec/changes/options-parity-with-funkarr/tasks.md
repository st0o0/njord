## 1. Per-concern sections

- [x] 1.1 Add specs for the new keys: `Njord:Polling:*`, `Njord:OpenMeteo:BaseUrl`, `Njord:Storage:PersistencePath` bind correctly and distinct `SectionName` values per options type
- [x] 1.2 Change the `SectionName` constants, rename `OpenMeteoBaseUrl` to `BaseUrl`, move the keys in `appsettings.json` and `appsettings.Example.json`, update all consumers and tests
- [x] 1.3 Add the legacy-key check to `NjordOptionsValidator` with specs (each of the five old keys, message names old and new key)
- [x] 1.4 Migrate old keys in the override file while loading (`ResilientJsonConfigurationSource`), warn once, persist the new shape on the next `ConfigWriter` write; specs for old file, new file, mixed file and corrupt file
- [x] 1.5 Update `ConfigWriter`/`ConfigManager` write targets and `ConfigReloadPolicy` property paths for the new sections

## 2. Registration helper

- [x] 2.1 Add `AddNjordOptions<T>` in `Njord.Core` with specs (binds, replaces collection defaults, registers validator, fails on invalid at start)
- [x] 2.2 Use it in `ConfigSetupContainer`, the feature setup containers and the analysis containers; delete the repeated bind lines

## 3. Access rule

- [x] 3.1 Audit every production injection of `IOptions<T>` and `IOptionsMonitor<T>` against `ConfigReloadPolicy`; fix the `IOptions<PollingOptions>` sites
- [x] 3.2 Add `OptionsAccessArchitectureSpec` (hot options not via `IOptions`, restart-required not via the monitor, explicit allow-list)
- [x] 3.3 Add `TestOptionsMonitor<T>` to `Njord.Tests.Shared` and use it where specs build monitors by hand

## 4. Compose, builder, docs

- [x] 4.1 Update `docker-compose.example.yml` to the new variable names and add the default-comment drift spec
- [x] 4.2 Update `docs/.vitepress/theme/builder/{serializer,types}.ts` (new keys, outdated-key report) and `docs/configuration`; update the e2e compose files and `e2e` docs
- [x] 4.3 Add the 0.5.0 key-mapping table to the docs and `AGENTS.md` (configuration layers, options conventions)

## 5. Verification

- [x] 5.1 Run `dotnet format whitespace`, build `Njord.slnx`, run every test project, `openspec validate --all --no-interactive`, `scripts/check-openspec-root.sh`
