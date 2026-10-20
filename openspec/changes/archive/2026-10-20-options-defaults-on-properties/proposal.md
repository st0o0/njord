## Why

Njord's scalar option defaults live in appsettings.json, not on the property. This means you can't see a default by reading the class — you need to check appsettings.json. FunkArr puts defaults on properties (discoverable) but duplicates them in appsettings.json. We can do better: defaults on properties, appsettings.json minimal (structure only, no duplicated values). appsettings.Example.json stays as the human-readable reference.

## What Changes

- Add property initializers to ~32 scalar properties across 13 options classes
- Strip appsettings.json Njord section: remove values that now match code defaults, keep section structure for config binding
- Update appsettings.Development.json: keep only genuine development overrides
- Relax arch spec rule: allow `new XOptions()` for scalar-only options classes (list-default classes still need AppSettingsDefaults)
- Update AGENTS.md convention: "Defaults on the property" replaces "scalar defaults in appsettings.json"
- AppSettingsDefaults stays for classes with list defaults (NjordOptions, AlertOptions, IndexOptions) because of ReplaceCollectionDefaults

## Non-goals

- Not changing list defaults (Horizons, threshold arrays) — they stay in code with ReplaceCollectionDefaults
- Not changing appsettings.Example.json (it's already a reference file)
- Not removing AppSettingsDefaults entirely (still needed for list-default classes)

## Capabilities

### New Capabilities

- `options-defaults`: Convention for where option default values live

### Modified Capabilities

_(none)_

## Impact

- 13 options classes: property initializers added
- appsettings.json: Njord section slimmed down
- AGENTS.md: convention updated
- Architecture tests: new XOptions() rule relaxed for scalar-only classes
- Tests: some files can switch from AppSettingsDefaults.Options<T>() to new T() (optional)
