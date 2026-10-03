## Why

Four feature libraries (Mqtt, Enrichment, Ingest, Sensors) lack dedicated test
projects — their specs live in the host test project `Njord.Tests`. FunkArr
achieves clean 1:1 mapping between production and test projects. Extracting
dedicated test projects improves test isolation, makes the test structure match
the production structure, and makes it clear which library each spec exercises.

## What Changes

- Create `Njord.Mqtt.Tests` with Mqtt specs moved from `Njord.Tests/Mqtt/`
- Create `Njord.Enrichment.Tests` with Enrichment specs moved from `Njord.Tests/Enrichment/`
- Create `Njord.Ingest.Tests` with Ingest specs moved from `Njord.Tests/Ingest/`
- Create `Njord.Sensors.Tests` with Sensors specs moved from `Njord.Tests/Sensors/`  
- `Njord.Tests` retains only host-specific tests: Configuration, Health, PollPipelineSpec
- Add all four new test projects to `Njord.slnx`
- Update AGENTS.md test project list and test count documentation

## Capabilities

### New Capabilities

- `test-project-extraction`: Extract Mqtt, Enrichment, Ingest, and Sensors test
  specs from Njord.Tests into dedicated per-library test projects with 1:1
  mapping to production projects.

### Modified Capabilities

(none)

## Non-goals

- Changing any test logic — this is purely structural (move files, update
  references).
- Adding new tests — the extracted tests remain identical.
- Moving Persistence specs from `Njord.Tests` — the `ForecastHistoryDtoSerializationSpec`
  is a Verify golden-master test that depends on the Enrichment library, which
  makes it better suited to stay in the host test project or move with Enrichment.

## Impact

- **New projects:** 4 test projects (Mqtt.Tests, Enrichment.Tests, Ingest.Tests,
  Sensors.Tests).
- **Modified projects:** `Njord.Tests` (files removed), `Njord.slnx` (projects added).
- **API budget:** Zero.
- **Risk:** Low — file moves with namespace updates. All tests must still pass
  after extraction.
