## Context

Config layers (`Program.cs`): `appsettings.json` → `appsettings.{Environment}.json` → environment variables → command line → `data/appsettings.Override.json` (resilient JSON source added last, so it has the highest priority). Option classes hold the defaults as property initializers; `ReplaceCollectionDefaults` rebinds configured lists so they replace the initializer value. FunkArr keeps baseline values in `appsettings.json` and registers options with `Bind(configuration.GetSection(...))` plus `ValidateOnStart`.

## Goals / Non-Goals

**Goals:** one inspectable source for scalar defaults, no duplicate in the example file, tests that exercise the shipped values.

**Non-Goals:** changing any default value, changing the override file or `ConfigWriter` semantics, moving list defaults, changing the Dockerfile.

## Decisions

1. **`appsettings.json`, not Dockerfile `ENV`.** The file is the lowest-priority layer, ships in the image and works in `dotnet run` and tests. `ENV` would only apply in the container, cannot express lists naturally and would sit at the same priority as the compose environment. Environment variables stay for deployment-specific values.
2. **Lists stay in code.** Index-based array merging makes defaults-in-file unsafe for any list an operator can shorten. A custom replacing configuration provider would fix it but is a much larger change; it can follow later if wanted. The scalar part removes the duplication problem for almost all defaults.
3. **No `required` members.** `required` would force object initializers at about 100 test call sites and does not protect against a missing file. Validators already reject zero or empty values for the options where that is invalid (poll interval, budget, URLs), and `ValidateOnStart` makes a missing section fail at startup.
4. **Shared test helper** `AppSettingsDefaults` in `Njord.Tests.Shared` loads the shipped `appsettings.json` (linked into the test output) and binds any options type, applying `ReplaceCollectionDefaults`. Specs that used `new XOptions()` for the default case use it; specs that set every relevant field explicitly keep their own construction.
5. **Golden master of default options** (one Verify snapshot of all bound option objects) is the one-time and permanent proof that moving the values changed nothing; the snapshot is first taken from the current code defaults, before the initializers are removed, and must stay identical afterwards.
6. **Order of work:** snapshot first (code defaults), then add the `Njord` section, then remove initializers per options class, each step green. This keeps every commit verifiable.
7. **`MqttOptions.DiscoveryPrefix`** is carried over unchanged; whether it is vestigial (no MQTT discovery exists) is out of scope here and noted for a follow-up.
8. **Case-insensitive dictionaries and nested option objects** keep their instance initializers (`new Dictionary(StringComparer.OrdinalIgnoreCase)`, `new()`), because they are structure, not default values.

## Risks / Trade-offs

- A host that does not load `appsettings.json` (custom test hosts) would see zeros; the helper and the host specs cover this, and validators fail loudly at start.
- Two places still hold defaults (file for scalars, code for lists); the spec and `AGENTS.md` state the rule so contributors know which goes where.
- Docs and the web config builder may restate defaults; they are checked in the tasks.

## Migration Plan

No operator action: values are identical. Operators who copied defaults from `appsettings.Example.json` keep working because the example keys remain valid overrides.
