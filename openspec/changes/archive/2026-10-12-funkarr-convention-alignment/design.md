## Context

A full FunkArr convention audit found the codebase deviates in logging setup,
message naming, response hierarchies, persistence mapping, and actor/state
naming. The project is 0.x so breaking renames are allowed via `refactor!:`.

## Goals / Non-Goals

**Goals:**
- Align all actor logging with FunkArr inline readonly pattern
- Rename all message types to match FunkArr VerbNoun/QueryNoun naming
- Complete missing response hierarchies (Failed variants, abstract bases)
- Dissolve DtoMapping classes into StateExtensions
- Fix actor/state naming mismatches
- Update AGENTS.md to match actual patterns

**Non-Goals:**
- No runtime behavior changes
- No new message semantics
- `FetchForecastFailed` keeps `PollFailureReason` (domain-typed, not Exception)
- `SubscribeInbound`'s `IActorRef` parameter stays (explicit subscription)
- No change to `Ack` — it's used for fire-and-forget acknowledgement, valid
  as FunkArr's "config" category

## Decisions

### D1: Execution order — logging first, then naming, then structure

Logging changes are purely mechanical (no type renames) and can be validated
independently. Message renames have the largest blast radius. DtoMapping
dissolution depends on the SchedulerState rename. Order:
1. Logging (C1–C3)
2. Actor/state naming (B3, B5)
3. DtoMapping dissolution (A5)
4. Message naming (B1, B2)
5. Response hierarchy completion (A1, A2)
6. File splitting (B6)
7. Documentation (B7)

### D2: Message renames use find-and-replace, not indirection

All renames are direct — old type deleted, new type created, all references
updated. No backwards-compat aliases, no `[Obsolete]` shims. 0.x policy.

### D3: FetchForecastFailed keeps domain-typed failure

`FetchForecastFailed` carries `PollFailureReason Reason` + `string? Detail`
instead of `Exception Cause`. This is intentional: failure modes are
domain-typed (RateLimited, TransportError, NoData) and the actor never catches
exceptions to populate this — it uses the failure mapper pattern. Adding
`Exception Cause` would require wrapping domain reasons in exceptions, losing
semantics.

### D4: Query responses use Result, command responses use Completed

FunkArr distinguishes: queries return `<Noun>Result`, commands return
`<VerbNoun>Completed`. This maps to the semantic difference: a query "finds"
a result, a command "completes" an action.

### D5: MqttState → MqttStatePublisher

The actor publishes state messages to MQTT. `MqttStatePublisher` describes its
role. `MqttState` collides with the FunkArr `*State` naming reserved for
immutable state records. The actor does not currently have a state record, but
if it gets one, `MqttStatePublisherState` is unambiguous.

## Risks / Trade-offs

- **[Risk] Large rename blast radius** → Mitigated: all renames are mechanical
  (find-replace on type names), build verifies completeness, 0.x allows breaks.
- **[Risk] Stale specs reference removed actors** → Resolved: REMOVED
  requirements for ShardRegion routing in the message-conventions delta spec.
