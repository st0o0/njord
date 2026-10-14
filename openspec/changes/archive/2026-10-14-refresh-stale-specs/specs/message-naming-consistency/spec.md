## MODIFIED Requirements

### Requirement: Response types use Completed/Failed naming
Query response records SHALL follow FunkArr naming: drop the `Query` prefix,
use `<Noun>Result` for success (not `<Noun>Completed`), and `<Noun>Failed` or `<Noun>NotAvailable` for the non-success case. Command response records SHALL use `<VerbNoun>Completed` and
`<VerbNoun>Failed` (plus `<VerbNoun>Rejected` for refusable commands), matching the command name exactly.

#### Scenario: Query response naming
- **WHEN** all query response records in `Njord.Messages` are inspected
- **THEN** each uses a `<Noun>Response` abstract base with a `<Noun>Result` subtype and a `<Noun>Failed` or `<Noun>NotAvailable` subtype (no `Query` prefix on any response type)

#### Scenario: Command response naming
- **WHEN** all command response records in `Njord.Messages` are inspected
- **THEN** each uses `<VerbNoun>Response` abstract base with `<VerbNoun>Completed` and `<VerbNoun>Failed` subtypes matching the command record name
