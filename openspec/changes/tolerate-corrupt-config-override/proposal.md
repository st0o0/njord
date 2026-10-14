## Why

The runtime override file `data/appsettings.Override.json` (written by the
configuration manager when settings are changed through the AdminService) is
loaded with the stock JSON configuration source. A truncated, empty, non-object
or otherwise unparseable file aborts startup with an unhandled
`InvalidDataException` from `Program.cs` (process exit 134), although the
`config-persistence` spec already promises a fallback. At runtime, a file that
turns corrupt is reloaded on a background thread: the exception disappears into
an unobserved task, the override values are silently dropped from
`IConfiguration` without a change notification, and the next AdminService
mutation fails because the writer cannot parse the file either. A single bad
byte on the Docker volume therefore takes the service down or leaves it in an
inconsistent state.

## What Changes

- An unreadable or unparseable override file no longer prevents startup: njord
  logs an Error naming the file and the problem and starts with the remaining
  configuration layers (`appsettings*.json`, environment variables). The file is
  left untouched.
- A runtime reload of an unreadable or unparseable override file keeps the last
  good override values (no silent fallback) and logs an Error.
- The next AdminService mutation moves the corrupt file aside to
  `appsettings.Override.json.corrupt-<UTC timestamp>` (logged as an Error), then
  writes a fresh override file that contains the last good override values plus
  the mutation, so the persisted file matches the running configuration and the
  user's data is never destroyed.
- The writer reads the override file with the same leniency as the
  configuration loader (comments, trailing commas), so every file njord can load
  can also be updated.
- Override writes stay atomic (temp file in the same directory + rename) and
  additionally flush the temp file to disk before the rename.
- Valid JSON with invalid option values still fails startup validation
  (fail fast is unchanged).

## Non-goals

- No change to option validation or `ValidateOnStart` behavior.
- No automatic repair of the corrupt file and no health-check or gRPC surface
  for the corrupt state (the Error log is the signal).
- No support for preserving JSON comments when njord rewrites the file.
- No change to the other configuration layers (`appsettings*.json`,
  environment variables) — a corrupt `appsettings.json` still fails startup.

## API budget

None. The change does not add or alter polling; Open-Meteo request volume is
unchanged.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `config-persistence`: the corrupt-file fallback becomes an Error log over the
  remaining layers; new requirements for keeping the last good values on a
  corrupt runtime reload and for preserving a corrupt file on the next mutation.
- `writable-options`: atomic write additionally flushes to disk; the writer
  accepts every override file the configuration loader accepts.

## Impact

- `src/Njord/Program.cs` — registers the tolerant override source.
- `src/Njord/Configuration/` — new tolerant JSON configuration source/provider,
  `CoreSetupContainer` and `NjordApplicationSetup` wiring.
- `src/Njord.Core/Configuration/ConfigWriter.cs` — corrupt-file handling,
  lenient read, durable atomic write; new shared override-file state.
- `src/Njord.Tests.Shared/ConfigHarness.cs`, `src/Njord.Core.Tests`,
  `src/Njord.Tests` — specs.
- `docs/configuration/index.md` — runtime override file section.
