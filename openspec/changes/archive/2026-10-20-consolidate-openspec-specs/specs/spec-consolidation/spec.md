## ADDED Requirements

### Requirement: Merge overlapping specs without losing requirements
When merging multiple specs into one, every unique requirement from the source specs SHALL be preserved in the target spec. Duplicate requirements (same rule stated differently) SHALL be deduplicated, keeping the most precise wording.

#### Scenario: Message-naming merge
- **WHEN** merging `message-naming`, `message-naming-consistency`, `message-naming-convention`, `message-response-naming` into `message-conventions`
- **THEN** `message-conventions` SHALL contain: VerbNoun command naming, QueryNoun query naming, response hierarchies (Completed/Failed/Rejected/NotAvailable), `Request*Failed` handshake failure naming, and `VerbNounResponse` base pattern

#### Scenario: Persistence merge
- **WHEN** merging `persistence-records`, `persistence-record-hygiene`, `persistence-funkarr-alignment`, `persistence-roundtrip-tests` into `persistence-records`
- **THEN** `persistence-records` SHALL contain: positional record style, `Persisted*State`/past-tense naming, DateTimeOffset/TimeSpan types, no `[JsonProperty]`, sealed immutable records, file-per-type layout, roundtrip test requirement, Verify wire-format snapshot requirement

### Requirement: Delete only fully-implemented stale specs
A spec SHALL only be deleted (without merging) when all its requirements are either fully implemented in code or already documented in AGENTS.md/CLAUDE.md.

#### Scenario: test-cleanup is fully done
- **WHEN** checking `test-cleanup` spec targets (`Njord.Integration.Tests/`, `Njord.E2E.Tests/`, `Njord.Ingest/`, `Njord.Egress/`, `Njord.Enrichment/`)
- **THEN** none of these directories SHALL exist in the codebase, confirming the spec is done

#### Scenario: xml-doc-cleanup is fully done
- **WHEN** scanning production code for `<summary>` XML doc tags
- **THEN** zero matches SHALL be found, confirming the spec is done

### Requirement: Rename for clarity when merging changes scope
When a spec is renamed to better reflect its merged content, the new name SHALL describe the broader topic.

#### Scenario: persistence-backoff rename
- **WHEN** `persistence-backoff` covers BackoffOnFailure supervision for persistent actors
- **THEN** it SHALL be renamed to `persistence-supervision` to reflect the broader supervision topic
