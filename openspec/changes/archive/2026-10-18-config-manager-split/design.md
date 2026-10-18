## Context

ConfigManager (486 LOC, 9 ctor params) is the config aggregation actor. Its write handlers (~200 LOC) perform validation, budget-guard projection, and file persistence — pure logic that doesn't use actor state, Akka APIs, or stream infrastructure. The reload mechanism is already decoupled: `IOptionsMonitor` callbacks fire when the config file changes on disk.

## Goals / Non-Goals

**Goals:**
- Extract write logic into `ConfigUpdateHandler` (~200 LOC)
- Reduce ConfigManager to ~285 LOC and 8 ctor params
- Keep the actor's message API unchanged

**Non-Goals:**
- Separate actor for writes (adds coordination overhead)
- Reducing IOptionsMonitor injections (they're ConfigManager's core job)

## Decisions

### D1: ConfigUpdateHandler is a plain class, not an actor

The write handlers don't use `Context`, `Self`, `Sender`, timers, or any Akka API. They take input, validate, call `IConfigWriter`, and return a result. A plain class is the right abstraction.

### D2: ConfigManager creates the handler internally

`ConfigUpdateHandler` is not resolved via DI — ConfigManager creates it in its constructor from the injected `IConfigWriter` and the options monitors it already has (for budget-guard projection). This avoids adding DI registration complexity.

### D3: Write handlers return the response, ConfigManager sends it

Each handler method returns the response record (success or failure). ConfigManager calls the handler and sends the result to `Sender`. This keeps the Akka message plumbing in the actor where it belongs.

```csharp
Receive<UpdateLocations>(msg => Sender.Tell(_updates.HandleUpdateLocations(msg, _scope, _polling)));
```

### D4: Budget-guard projection stays in the handler

The `BudgetRejection()` helper projects whether a scope change would exceed the budget. It reads current scope + polling options — the handler receives these as method parameters from ConfigManager, not as injected dependencies.

## Risks / Trade-offs

- **[Risk]** Handler needs access to current options for budget projection → **Mitigation**: ConfigManager passes current option values as method parameters. The handler is stateless.
- **[Risk]** Test structure changes → **Mitigation**: ConfigManagerSpec write tests already test through message send/receive. The handler can be unit-tested separately for validation edge cases, but existing actor tests continue to work unchanged.
