## Purpose

Defines consistent naming for response records: query responses drop the Query prefix and use Result/Failed, command responses use Completed/Failed matching the command name.

## Requirements

### Requirement: Response types use Completed/Failed naming
Query response records SHALL follow FunkArr naming: drop the `Query` prefix,
use `<Noun>Result` for success (not `<Noun>Completed`), `<Noun>Failed` for
failure. Command response records SHALL use `<VerbNoun>Completed` and
`<VerbNoun>Failed`, matching the command name exactly.

#### Scenario: Query response naming
- **WHEN** all query response records in `Njord.Messages` are inspected
- **THEN** each uses `<Noun>Response` abstract base with `<Noun>Result` and `<Noun>Failed` subtypes (no `Query` prefix on any response type)

#### Scenario: Command response naming
- **WHEN** all command response records in `Njord.Messages` are inspected
- **THEN** each uses `<VerbNoun>Response` abstract base with `<VerbNoun>Completed` and `<VerbNoun>Failed` subtypes matching the command record name
