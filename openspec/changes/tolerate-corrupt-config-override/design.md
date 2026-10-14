## Context

See proposal.md (Why). Verified current behavior (2026-10-14, .NET 10.0.12):

- **Startup.** `Program.cs` calls `builder.Configuration.AddJsonFile(OverridePath, optional: true, reloadOnChange: true)`.
  `ConfigurationManager` loads a source as soon as it is added, so a corrupt file throws
  `InvalidDataException("Failed to load configuration from file ...")` straight out of `Program.cs`
  line 30; `dotnet run` exits with 134. Truncated JSON, an empty or whitespace-only file and a
  non-object root (`[1]` → "Top-level JSON element must be an object") all fail this way. An unreadable
  file (permission denied / locked) throws the raw `UnauthorizedAccessException`/`IOException`, because
  the stock `FileConfigurationProvider` opens the stream outside its own `try`. JSON with comments and
  trailing commas loads fine.
- **Runtime.** On a file-watcher reload the provider catches the parse error, clears its data and
  rethrows from the change callback, which runs on a pool task: the exception becomes an unobserved task
  exception (no log, no crash). `IConfiguration` silently loses all override values while no change
  token fires, so `IOptionsMonitor` keeps stale cached values until some later reload. The next
  `IConfigurationRoot.Reload()` (every successful `ConfigWriter` mutation calls it) throws
  `InvalidDataException`.
- **Writer.** `ConfigWriter` already writes atomically (`<path>.tmp` in the same directory, then
  `File.Move(overwrite: true)`), but does not flush the temp file to disk. It parses the existing file with
  strict `JsonNode.Parse`, so a corrupt file — or a hand-edited one with comments — makes every mutation
  throw (`UpdateSettingsFailed` etc. on the gRPC side).
- **Validation.** Valid JSON with invalid values fails `ValidateOnStart`; specs
  (`enrichment-config-validation`, `location-model-resolution`, ...) require that, and this change keeps it.

## Goals / Non-Goals

**Goals:** corrupt or unreadable override file never stops startup; corrupt runtime reload keeps the last
good values; never silently destroy the user's file; writer and loader agree on what is valid; every
failure is logged at Error.

**Non-Goals:** tolerance for the other configuration layers; surfacing the corrupt state through health
checks or gRPC; preserving comments.

## Decisions

### 1. A tolerant JSON configuration source in the host (chosen) instead of a pre-validation step

`ResilientJsonConfigurationSource`/`Provider` (`src/Njord/Configuration/`) derive from
`JsonConfigurationSource`/`JsonConfigurationProvider` and override:

- `Load(Stream)`: read the bytes, delegate to the base parser on a `MemoryStream`; on success remember the
  raw text as the last good content; on failure keep `Data` unchanged and report the error.
- `Load()`: wrap the base call to also catch open errors (`IOException`, `UnauthorizedAccessException`).

The base `ReloadOnChange` stays off; the provider registers its own `ChangeToken.OnChange` watch that
waits `ReloadDelay` and calls the overridden `Load()`, so watcher reloads go through the same catch. Since
`Load` no longer throws, the base still raises the change token after a failed reload — options are
re-bound from the unchanged data (a harmless no-op snapshot for `ConfigManager`).

Alternatives:
- *Pre-validate in `Program.cs`* (parse once, skip `AddJsonFile` on failure): fixes startup only; a runtime
  reload still clears data silently and the next `Reload()` still throws. Rejected.
- *`OnLoadException = ctx => ctx.Ignore = true`*: the stock provider clears its data on a failed reload
  (silent fallback, the opposite of "keep last good"), does not cover open errors at startup, and gives no
  logger at that point. Rejected.
- *Own `ConfigurationProvider` from scratch with own watcher and parser*: the JSON flattening parser is
  internal; re-implementing it is more code and risk than subclassing. Rejected.

### 2. Shared `ConfigOverrideFile` state in `Njord.Core`

`ConfigOverrideFile` (path, last good content, error reporting) lives in `Njord.Core/Configuration` so
`ConfigWriter` (Core) and the host provider share it without Core depending on
`Microsoft.Extensions.Configuration.Json`. `Program.cs` creates it, hands it to the source and registers
it as a singleton; `CoreSetupContainer` passes it to `ConfigWriter` (with `TryAdd` of a default for the
test host that never adds the source).

Logging: the configuration is built before DI/Serilog exist, so `ConfigOverrideFile` buffers failures and
`NjordApplicationSetup` attaches an `ILogger<ConfigOverrideFile>` right after the app is built (before
`StartAsync`, so before `ValidateOnStart`); buffered failures are flushed then, later ones are logged
immediately. A new hosted service was considered but adds a type for a one-line hand-off.

### 3. Corrupt file on disk: leave it at startup, move it aside on the next write

- At startup and on runtime reloads the file is left untouched and logged; nothing is written, which keeps
  "no override file is created at startup" and lets the user repair the file in place (the watcher picks
  the repair up).
- The next mutation cannot merge into unparseable content. Options considered:
  - *Refuse mutations until the user fixes the file*: safest for the bytes, but leaves the HA admin UI
    broken until someone edits the Docker volume by hand. Rejected.
  - *Overwrite with the mutation only*: destroys user data and, after a runtime corruption, silently drops
    the last good values that are still running. Rejected.
  - *Move aside, then write last good content + mutation* (chosen): `File.Move` to
    `appsettings.Override.json.corrupt-<yyyyMMddTHHmmssfffZ>` (from `TimeProvider`, no overwrite — a
    collision fails the mutation instead of clobbering), Error log naming both paths, then the normal merge
    into the last good content. The persisted file equals the running configuration (base layers + last
    good overrides + mutation) and the original bytes stay on disk.
- Only a parse failure triggers the move; a read failure (locked/permission) propagates as today and the
  mutation fails without touching anything.

### 4. Writer parses leniently and flushes before the rename

`JsonNode.Parse` with `CommentHandling.Skip` and `AllowTrailingCommas = true` (the loader's settings). The
temp file is written through a `FileStream` and `Flush(flushToDisk: true)` before `File.Move(overwrite:
true)`, so a power loss leaves either the old or the complete new file.

## Risks / Trade-offs

- [Error log is the only signal of a corrupt file] → message names the file, the reason and the next step
  ("fix or delete it; the next configuration change moves it aside").
- [Last good content is the raw text of the last successful load; an external edit that is valid replaces
  it] → intended: valid external edits are authoritative, as today.
- [`.corrupt-*` files accumulate if the file keeps getting corrupted] → each is a user-data copy and is
  never deleted automatically; unlikely in practice.
- [The provider subclasses framework types (`JsonConfigurationProvider.Load(Stream)`, `Load()`)] → both are
  public virtual API; covered by specs that would fail on a framework behavior change.
