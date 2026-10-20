## Context

Clients own variable lists, handlers only name domains, and Points mix required and optional fields. The goal is a single flow: handlers declare parameters, resolution unions them, the gateway passes them to thin clients, the pipeline verifies presence before dispatch.

## Goals / Non-Goals

**Goals:** rename, declarative requirements, nullable Points, per-domain ForecastDays, data-presence guard.
**Non-Goals:** polling, topology, proto, new enrichments (see proposal).

## Decisions

1. **Name `Njord.Forecast`** over `Njord.Domain`, `Njord.Abstractions`, `Njord.Types`: it describes what the project contains.
2. **Param enums are `[Flags]` with bit shifting**: enables `|` union, compile-time safety, and no magic strings in Core.
3. **`WeatherParam : long`** (64 bits for 27+ variables with growth room); the other enums are `int` (32 bits suffice).
4. **`DomainRequirements` is a flat record struct** holding all four param types. The coupling is acceptable because `DomainKind` already defines the closed set.
5. **`HasFields()` lives on the Point** in `Njord.Forecast`, which owns both the Point and its param enum.
6. **`ToApiNames()` lives in `Njord.OpenMeteo`**: explicit switch, one arm per member, default throws `ArgumentOutOfRangeException` (njord convention); each mapping has a spec pinning every member.
7. **Validation in `ProjectionEngine`** before handler dispatch: it is where handlers are already dispatched; handlers no longer check defensively.
8. **All Point fields nullable except `ValidFrom`**: presence depends on the request.
9. **Per-domain options, FunkArr-style**: flat sections, defaults in property initializers, `AddNjordOptions<T>` with `ValidateOnStart`.
10. **`Triggers` removed**: derived through `Requirements.Requires(domain)`.

## Risks / Trade-offs

- Nullable Points ripple through handlers, gRPC and MQTT mappings. Mitigation: chunk 3 is isolated and validated against all suites before behavior changes.
- Weather spread variable and `IncludeSolar` must map to flags so call weight (4) is unchanged. Mitigation: a spec pins the default resolved variable count.
- Rename churn. Mitigation: chunk 1 is rename-only, using `git mv` to preserve history.
- Points are not persisted, so no persistence migration is needed. Breaking changes are acceptable while 0.x.
