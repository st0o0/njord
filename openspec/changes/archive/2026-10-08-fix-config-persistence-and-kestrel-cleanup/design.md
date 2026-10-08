## Context

`AdminGrpcService` mutation RPCs (`SetEnrichment`, `SetSettings`, `SetLocations`,
`SetBudget`) clone the current `NjordOptions`, apply field-by-field mutations, and
persist via `ConfigPersistence.SaveAsync()` to `data/njord-config.json`. This file
is loaded via `AddJsonFile(..., reloadOnChange: true)` and triggers
`IOptionsMonitor<NjordOptions>.OnChange`.

Two bugs prevent mutations from taking effect:

1. `ConfigPersistence` serialises `NjordOptions` directly — the file contains
   `{"Enrichment": ...}` instead of `{"Njord": {"Enrichment": ...}}`. Since options
   are bound via `GetSection("Njord")`, the persisted keys never match.

2. Even with a correct section wrapper, `AddJsonFile` is called *after*
   `WebApplicationBuilder`'s default env var provider. Because last-added wins in
   .NET Configuration, the override file *should* win — but only if the keys match
   (blocked by bug #1).

Separately, `Program.cs` contains a verbose `ConfigureKestrel` block with branching
on `ASPNETCORE_URLS`, magic port numbers, and a dead `GrpcOptions` class.

## Goals / Non-Goals

**Goals:**

- Runtime config mutations take effect regardless of whether baseline config comes from
  env vars, `appsettings.json`, or `appsettings.Development.json`
- `applied=true` in a mutation response guarantees the subsequent `GetConfig` returns
  the mutated value
- Standard .NET config layering — no custom config concepts
- Kestrel port binding via declarative `appsettings.json`, not code
- Simpler `AdminGrpcService` — no manual `CloneOptions` + field mapping

**Non-Goals:**

- Adopting an external writable-options NuGet package
- Changing the AdminService proto contract
- Supporting concurrent writers from different processes

## Decisions

### Decision 1: Own `IWritableOptions<NjordOptions>` implementation

**Choice:** Implement `WritableNjordOptions` in `Njord.Core`, inspired by the
community `IWritableOptions` pattern (WritableOptions.Net, Awesome.Net.WritableOptions).

**Why not a NuGet package:** The implementation is ~60 lines. The packages target
`appsettings.json` in the app root; njord needs `data/appsettings.Override.json` in a
Docker volume. Owning the code avoids an unnecessary dependency and the path
customisation is trivial.

**Interface:**

```csharp
public interface IWritableOptions<out T> where T : class, new()
{
    T Value { get; }
    NjordOptions Update(Action<NjordOptions> applyChanges);
}
```

`Update` returns the post-mutation options snapshot for the `ConfigResponse`.

**Mechanism:**

```
Update(applyChanges)
  ├─ await _semaphore.WaitAsync()
  ├─ clone = DeepClone(_optionsMonitor.CurrentValue)
  ├─ applyChanges(clone)
  ├─ wrapper = { "Njord": clone }
  ├─ json = JsonSerializer.Serialize(wrapper)
  ├─ File.WriteAllText(tmpPath, json)
  ├─ File.Move(tmpPath, overridePath, overwrite: true)
  ├─ _configRoot.Reload()
  └─ return _optionsMonitor.CurrentValue
```

`_configRoot` is `IConfigurationRoot` (the builder's `Configuration` cast).

**Alternatives considered:**

- *In-memory `IConfigurationProvider`*: Immediate update without file I/O, but no
  persistence across restarts. Rejected — mutations must survive container restarts.
- *`IPostConfigureOptions<NjordOptions>`*: Bypasses the config system entirely.
  Rejected — `IOptionsMonitor.OnChange` wouldn't fire, breaking `StreamConfig`.

### Decision 2: Override file at `data/appsettings.Override.json`

**Choice:** Persist mutations to `data/appsettings.Override.json`, loaded as the
last config source in `Program.cs`.

**Why this path:**

- `data/` is the Docker volume mount point — survives container restarts
- `appsettings.Override.json` follows .NET convention for layered config files
- Loaded last → highest priority → overrides env vars

**Config chain after this change:**

```
1. appsettings.json                    (defaults + Kestrel endpoints)
2. appsettings.{Environment}.json      (dev overrides)
3. Environment variables               (Docker baseline)
4. Command-line args
5. data/appsettings.Override.json      (runtime mutations — HIGHEST)
```

**Why not write to `appsettings.json`:** It's baked into the Docker image and
represents defaults. Writing to it would mix authored defaults with runtime state.

### Decision 3: AdminService uses `IWritableOptions.Update()`

**Choice:** Replace the `CloneOptions` + field-by-field mutation + `SaveAsync` pattern
with a single `IWritableOptions.Update(opt => { ... })` call per mutation RPC.

**Before (SetEnrichment, simplified):**

```csharp
var options = CloneOptions(_optionsMonitor.CurrentValue);
options.Enrichment.Alerts.Enabled = request.Alerts.Enabled;
await _persistence.SaveAsync(options);
return Success(options, budget);
```

**After:**

```csharp
var result = await _writableOptions.Update(opt =>
{
    if (request.Alerts is { } alerts && alerts.HasEnabled)
        opt.Enrichment.Alerts.Enabled = alerts.Enabled;
});
var budget = BudgetCalculator.Validate(result);
return Success(result, budget);
```

The `CloneOptions` method and `ConfigPersistence` class are removed. The mutation
lock moves into `WritableNjordOptions`.

**Budget validation:** Validation still happens after mutation. If budget is exceeded,
the mutation must be rolled back. Two approaches:

- *Validate-then-write*: `Update` takes a `Func<NjordOptions, bool>` that returns
  false to abort. Adds complexity to the interface.
- *Write-then-check*: Write always succeeds; if budget fails, write again with the
  original values. Simpler but two file writes on rejection.
- *Chosen:* Keep budget validation in AdminService. `Update` always writes. If budget
  check fails after `Update`, call `Update` again to restore. This matches the current
  pattern where `SaveAsync` always writes and rejection is a separate concern. In
  practice, budget rejection is rare (config change required to hit it), so the
  double-write cost is negligible.

### Decision 4: Kestrel endpoints via appsettings.json

**Choice:** Move Kestrel endpoint configuration to `appsettings.json`:

```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": { "Url": "http://+:8080", "Protocols": "Http1" },
      "Grpc": { "Url": "http://+:8081", "Protocols": "Http2" }
    }
  }
}
```

`ConfigureKestrel` reduces to:

```csharp
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MinResponseDataRate = null;
});
```

**Why `MinResponseDataRate` stays in code:** Kestrel's JSON config does not support
`Limits.MinResponseDataRate`. It must be set programmatically. Setting it globally
(not per-endpoint) is acceptable because the HTTP/1.1 port only serves short health
requests where the rate limit is irrelevant.

**Port overrides:** Docker users override via env vars:
`Kestrel__Endpoints__Grpc__Url=http://+:9081`. This replaces the custom
`Njord:Grpc:Port` path.

**`GrpcOptions` removal:** The class only held `Port` and is unused elsewhere.
Deleted without replacement.

### Decision 5: No automatic migration for existing njord-config.json

**Choice:** The project is 0.x. Existing `data/njord-config.json` files are silently
ignored after this change. No migration code.

**Why:** The file only exists in dev/test environments. Docker deployments use env vars
for baseline config. The few users with a `njord-config.json` can rename it to
`appsettings.Override.json` and wrap the content in `{"Njord": {...}}`.

## Risks / Trade-offs

**[Budget validation requires double-write on rejection]** → Mitigation: Budget
rejection is rare (requires a config change that pushes over the limit). The second
write restores the original state. This is simpler than adding abort semantics to
`IWritableOptions.Update`.

**[IConfigurationRoot.Reload() is synchronous]** → Mitigation: Mutations are
infrequent (user-initiated via ha-njord config panel). The reload is fast (one JSON
file read). No performance concern.

**[Global MinResponseDataRate = null]** → Mitigation: Only affects the HTTP/1.1
health endpoint which serves sub-millisecond responses. The rate limit was never
meaningful there.

**[Kestrel port override path changes]** → Mitigation: `Njord:Grpc:Port` → 
`Kestrel__Endpoints__Grpc__Url`. This is a breaking change for the port override
mechanism, but the default ports (8080, 8081) are unchanged. Docker users who
override ports need to update their env vars. Documented in proposal.

## Open Questions

None — all decisions are straightforward and align with .NET conventions.
