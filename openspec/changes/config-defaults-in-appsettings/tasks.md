## 1. Proof before moving

- [ ] 1.1 Add `AppSettingsDefaults` to `Njord.Tests.Shared` (links and binds the shipped `appsettings.json`, applies `ReplaceCollectionDefaults`) with a spec
- [ ] 1.2 Add a Verify golden master of all default option objects taken from the current code defaults
- [ ] 1.3 Add a spec that binds the shipped file and runs every validator without failures

## 2. Move the defaults

- [ ] 2.1 Add the complete `Njord` section (scalars, flags, enums, durations) to `Njord/appsettings.json`
- [ ] 2.2 Remove the scalar initializers from the polling, budget, OpenMeteo, storage and persistence options and keep the golden master identical
- [ ] 2.3 Remove them from consensus, alerts (scalar thresholds), derived, trends, sensors and indices options
- [ ] 2.4 Remove them from `MqttOptions` (port, base topic, discovery prefix) and `NjordOptions` (forecast days); keep list and structural initializers
- [ ] 2.5 Replace default-reliant `new XOptions()` uses in tests with `AppSettingsDefaults`

## 3. Example file, docs and rules

- [ ] 3.1 Reduce `appsettings.Example.json` to deployment-specific keys and add the duplicate-detection spec
- [ ] 3.2 Update the docs configuration pages and the web config builder defaults to reference `appsettings.json`; document "reset means falling back to the file"
- [ ] 3.3 Update `AGENTS.md` (configuration layers: `appsettings.json` now carries the `Njord` defaults; list defaults stay in code)

## 4. Verification

- [ ] 4.1 Spec that a configured list replaces its default and a scalar override wins over the file
- [ ] 4.2 Run `dotnet format whitespace`, build `Njord.slnx`, run every test project, `openspec validate --all --no-interactive`, `scripts/check-openspec-root.sh`
