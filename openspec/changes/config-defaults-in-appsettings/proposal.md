## Why

Default option values are hard-coded as property initializers in about 45 places in `Njord.Core/Configuration`, while `appsettings.Example.json` (13 KB) repeats them by hand and the shipped `appsettings.json` has no `Njord` section at all. Operators cannot see the real defaults, the two copies drift, and "reset to defaults" has no inspectable meaning. FunkArr keeps its baseline values in `appsettings.json` and uses plain `Bind` plus `ValidateOnStart`; njord should do the same for the values that can live there.

## What Changes

- The shipped `appsettings.json` gets a complete `Njord` section holding every scalar and flag default (poll and discovery interval, forecast days, request budget limits, consensus method/trim/quorum, alert scalar thresholds, sensor staleness, Open-Meteo base URL, persistence provider and path, MQTT port/base topic, index and trend toggles).
- Option classes drop the initializers of those scalar and flag values; they keep initializers only for list-valued defaults (`Horizons`, `Parameters.Groups`, threshold arrays) and for containers that need an instance (dictionaries with a comparer, nested option objects).
- List-valued defaults stay in code on purpose: .NET configuration merges arrays by index, so a shorter override on top of a default list in `appsettings.json` would keep the old tail (`[6,24]` over `[3,6,12,24,48,72]` becomes `[6,24,12,24,48,72]`). `ReplaceCollectionDefaults` keeps solving this for lists.
- Tests build options from the shipped `appsettings.json` through a shared helper instead of relying on `new XOptions()` defaults; a spec binds and validates the shipped file and a golden master pins the resulting default options.
- `appsettings.Example.json` is reduced to the non-default, deployment-specific keys (locations, models, MQTT broker, overrides) with comments; the docs configuration reference points at `appsettings.json` as the single source of defaults.
- Deployment-specific values stay in environment variables (docker-compose); no defaults are baked into the Dockerfile as `ENV`.
- "Reset to defaults" is documented as "remove the override key, the value comes from `appsettings.json`".

## Capabilities

### New Capabilities
- `config-defaults`: where default option values live, how lists are treated, and how the shipped defaults are verified.

### Modified Capabilities
<!-- none: existing config specs describe behaviour that stays the same -->

## Impact

- `Njord/appsettings.json`, `appsettings.Example.json`, all `*Options` classes in `Njord.Core/Configuration`, `Njord.Tests.Shared` helper, test projects that instantiate options (about 100 call sites), `AGENTS.md` (configuration layers section), `docs/` configuration pages.
- No behaviour change when the shipped file is present: the values are identical; the golden master is the proof.
- Not breaking for operators; commit type `refactor:`. Depends on nothing, independent of `consistent-mappings`.
