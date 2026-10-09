# message-naming-consistency Specification

## Purpose
TBD - created by archiving change persistence-messages-cleanup. Update Purpose after archive.
## Requirements
### Requirement: Response types use Completed/Failed naming
Query and command response records SHALL follow the `*Completed`/`*Failed` naming
convention (matching FunkArr's pattern), not `*Found`/`*NotFound` or inconsistent `*Result`
suffixes.

#### Scenario: Response naming is consistent
- **WHEN** all response records in `Njord.Messages` are inspected
- **THEN** each query/command response hierarchy uses an abstract base named
  `VerbNounResponse` with sealed subtypes `VerbNounCompleted` and `VerbNounFailed`

### Requirement: Query and response definitions are co-located
A query record and its response hierarchy SHALL live in the same project and file. A query
in `Njord.Messages` SHALL NOT have its response hierarchy in a feature project.

#### Scenario: No split query/response definitions
- **WHEN** a query record is defined in `Njord.Messages`
- **THEN** its corresponding response abstract base and Completed/Failed subtypes are in
  the same file in `Njord.Messages`

### Requirement: No messages nested inside actor classes
Message records used for inter-actor communication SHALL be defined as top-level types in
`Njord.Messages` or `Njord.Persistence`, not nested inside an actor class.

#### Scenario: Actor class contains no nested message records
- **WHEN** any actor class (`*Actor.cs`) is inspected
- **THEN** it contains no `public sealed record` or `internal sealed record` definitions
  that serve as inter-actor messages

