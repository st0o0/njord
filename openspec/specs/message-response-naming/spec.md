## Purpose

Defines response hierarchy naming for commands that return a result (`VerbNounResponse`). Query responses are defined by `message-conventions` and `message-naming-convention` (`<Noun>Response` without the `Query` prefix).
## Requirements
### Requirement: Command responses with results SHALL follow VerbNounResponse naming

Commands that return a result (not fire-and-forget) SHALL have a response
named `VerbNounResponse` matching the command name.

#### Scenario: TriggerImmediatePoll has proper response hierarchy
- **WHEN** the system defines `TriggerImmediatePoll`
- **THEN** its response base is `TriggerImmediatePollResponse` (not `TriggerPollResult`)

