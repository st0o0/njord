## Why

The Config Builder's import function accepts `appsettings.json` and raw environment variables, but users commonly copy-paste from their `docker-compose.yml` which wraps env vars in YAML list syntax (`- Njord__Key=Value`) with leading whitespace. The `autoImport` function doesn't recognise this format — `importFromEnvVars` filters for lines starting with `Njord__` and silently returns an empty config. Users must manually strip the YAML formatting before importing.

## What Changes

- Extend the `autoImport` detection in the Config Builder serializer to recognise docker-compose `environment:` blocks.
- Add a `importFromCompose` function that strips YAML list markers and whitespace from env var lines before delegating to the existing `importFromEnvVars`.
- Update the import textarea placeholder text to mention docker-compose as a supported input format.

## Non-goals

- Full YAML parsing — we only need to extract `- KEY=VALUE` lines, not interpret the full compose schema.
- Importing non-environment compose fields (image, volumes, ports, etc.).
- Validation of the compose YAML structure.

## Capabilities

### New Capabilities

_(none — this extends an existing capability)_

### Modified Capabilities

- `config-builder`: Add docker-compose environment block as a recognised import format alongside JSON and raw env vars.

## Impact

- `docs/.vitepress/theme/builder/serializer.ts` — new `importFromCompose` function, updated `autoImport` detection.
- `docs/.vitepress/theme/builder/ConfigBuilder.vue` — updated placeholder text for the import textarea.
- No backend/service code changes. No API budget impact (docs-only change).
