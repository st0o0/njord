## 1. Selection to Core

- [ ] 1.1 Add `ParameterSelection` and `ParameterResolutionException` in `Njord.Core/Configuration` with specs (defaults, case-insensitive groups, unknown group/extra/exclude, empty result, collected errors, message text identical to today)
- [ ] 1.2 Point `ParametersOptions.Resolve()`, `NjordOptionsValidator`, `PollingOptionsValidator` and `BudgetCalculator` at `ParameterSelection`; remove `Resolve` and `ParameterResolutionException` from Weather

## 2. Registry as typed fields

- [ ] 2.1 Add catalog specs first: total entry count, uniqueness by API name and granularity, hourly and daily split per group, `weather_code` in both granularities
- [ ] 2.2 Rewrite `ParameterRegistry` as one `static readonly` field per parameter registering into the catalog; remove the static constructor and `Require*`; keep every existing property name
- [ ] 2.3 Keep `GetByApiName` for deserialization only; verify no production string literal calls it

## 3. Identity-only ParameterDef

- [ ] 3.1 Switch `OpenMeteoClient` unit validation (or `OpenMeteoUnitCheck` if `extract-actor-mappings` is applied) to unit-based rules; add a spec proving the checked parameter set equals the old device-class set
- [ ] 3.2 Remove `DeviceClass` from `ParameterDef` and the registry specs that asserted it

## 4. MQTT keys

- [ ] 4.1 Add `ParameterMqttKeys` and `ToMqttKey()` in `Njord.Mqtt/Mapping` with a spec that every catalog entry has a key and the keys equal the former `JsonKey` values
- [ ] 4.2 Replace `JsonKey` uses in `ForecastPayloadMappings`, `ConsensusPayloadMappings` and `TopicScheme`; remove `JsonKey`; MQTT golden masters stay unchanged

## 5. Enforcement and docs

- [ ] 5.1 Remove the `ParameterRegistry` entry from the `EnumConversionArchitectureSpec` allow-list and the enum-convention note in `AGENTS.md`
- [ ] 5.2 Update `AGENTS.md` (Weather description, parameter conventions) and docs that mention `JsonKey`/device class for parameters
- [ ] 5.3 Run `dotnet format whitespace`, build `Njord.slnx`, run every test project, `openspec validate --all --no-interactive`, `scripts/check-openspec-root.sh`
