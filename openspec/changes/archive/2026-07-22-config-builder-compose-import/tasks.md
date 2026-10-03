## 1. Compose import function

- [x] 1.1 Add `importFromCompose` function to `docs/.vitepress/theme/builder/serializer.ts` — strip leading whitespace and `- ` prefix from each line, then delegate to `importFromEnvVars`
- [x] 1.2 Update `autoImport` detection order: JSON → compose (lines matching `/^\s*-\s+\w+__/`) → raw env vars

## 2. UI update

- [x] 2.1 Update the import textarea placeholder in `docs/.vitepress/theme/builder/ConfigBuilder.vue` to mention docker-compose as a supported format

## 3. Validation

- [x] 3.1 Start the docs dev server (`npm run dev` from `docs/`), open the Config Builder, paste a docker-compose environment block, and verify it populates all fields correctly
- [x] 3.2 Verify existing imports still work: paste raw env vars and appsettings.json — both must still import correctly
