## Context

The Config Builder's `autoImport` function in `serializer.ts` detects input format by checking if the text starts with `{` (JSON) or falls back to `importFromEnvVars` (expects lines starting with `Njord__`). Docker-compose environment blocks use YAML list syntax with whitespace and `- ` prefixes that don't match either detector.

The existing `importFromEnvVars` function is already capable of parsing `KEY=VALUE` lines — compose input just needs pre-processing to strip the YAML wrapping.

## Goals / Non-Goals

**Goals:**
- Accept a pasted docker-compose snippet (full service block or just the `environment:` section) and populate the builder.
- Reuse `importFromEnvVars` for the actual parsing — only add a stripping layer.

**Non-Goals:**
- Full YAML parser or dependency on a YAML library.
- Importing non-environment compose fields (image, volumes, ports, networks).
- Handling compose variable substitution (`${VAR:-default}`).

## Decisions

### Detection: line-pattern matching, not YAML parsing

Compose input is detected by scanning for lines matching `- Njord__` (with optional leading whitespace). This avoids adding a YAML parser dependency for what is essentially a string-stripping operation.

**Alternative considered:** Regex for `environment:` keyword — rejected because users might paste just the env var list without the `environment:` header, and it's fragile if other YAML keys contain "environment".

**Chosen approach:** Check if any line in the input matches `/^\s*-\s+\w+__/` or `/^\s*-\s+Njord__/`. This catches both `- Njord__Key=Value` and bare `- KEY=VALUE` compose syntax.

### Stripping: regex per-line, then delegate

Each line is processed with: strip leading whitespace, strip `- ` prefix, then pass the cleaned lines to `importFromEnvVars`. This keeps all the actual config-parsing logic in one place.

### Detection order in `autoImport`

1. Starts with `{` → JSON
2. Contains lines matching `- Njord__` pattern → Compose (strip, then delegate to env-var parser)
3. Fallback → raw env vars

## Risks / Trade-offs

- **[Ambiguous input]** A user pastes a mix of raw env vars and compose-style lines → Mitigation: the compose detector is checked second, so if the input has raw `Njord__` lines without `- ` prefix, it falls through to the existing env-var parser correctly.
- **[Incomplete compose block]** A user pastes the entire compose file including non-njord services → Mitigation: `importFromEnvVars` already filters for `Njord__`-prefixed lines, so other services' env vars are ignored.
