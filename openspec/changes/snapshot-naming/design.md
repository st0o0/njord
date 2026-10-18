## Context

The word is used in three senses: Akka persistence state (the correct one), point-in-time views of data (`SensorSnapshot`, `ConfigSnapshot`, `*HealthSnapshot`) and computation results or accumulators (`ConsensusSnapshot`, `ModelSnapshot`). FunkArr has no such types; its persisted state is `Persisted<Name>State`. The earlier `persistence-mapping-and-enums` change moved `GetSnapshot()` into mapping classes; this change renames it.

## Decisions

1. **Names describe content, not timing.** `ModelForecasts` is the stream-side accumulator of the latest forecast per (location, model) with its `HasChanged` flag, not a snapshot of anything; `LocationConsensus` is a computed result. `SensorReadings` and `EffectiveConfig` are the readings and the config as they are now. `*Health` records are immutable state values, kept free of the `State` suffix that is reserved for actor state.
2. **`LocationSensorReadings` for the map** pushed by the SensorHub (`ImmutableDictionary<LocationId, SensorReadings>`), because `SensorReadings` is per location and the plural of it would read as a collection of the single type.
3. **Rename with the IDE-grade tooling, not text replace, for code.** Use Roslyn/IDE rename (or `dotnet`-based refactoring) per type so overloads, `nameof` and XML-free doc comments follow; then a repo-wide check for leftover words in comments, strings and identifiers (`snapshot`, `Snapshot`) with Akka terms allow-listed.
4. **File names follow type names** (one type per file convention); Verify `.verified.*` files that embed a type name are renamed with `git mv` so history survives, and their content must not change.
5. **Main specs get a mechanical, names-only update** (25 files). The behaviour of every requirement is unchanged, so no delta spec is written for them; the change's only delta is the new `type-naming` capability. The two capability directories named `consensus-snapshot` and `model-snapshot` keep their directory names to avoid breaking references; their purpose text names the new types.
6. **DTO collision.** `Njord.Messages.Config.ParameterSelection` becomes `ParameterScope` (it is the scope's parameter part: groups, extra, exclude), and the using-alias/qualified names added for the clash are removed.
7. **Architecture rule.** `NamingArchitectureSpec` scans type names of all production assemblies for `Snapshot` and for duplicate simple names between `Njord.Messages` and any other production assembly; no allow-list.

## Risks / Trade-offs

- A large mechanical diff makes review noisy; mitigated by doing it per type in separate commits-in-one-change steps (each step green) and by the byte-identical golden masters.
- External consumers: none of these types cross the wire (protos keep their names), so ha-njord is unaffected.
- Mixed unrelated edits in the working tree (the user's six uncommitted files) can conflict with a bulk rename; the apply step must leave those files alone or ask.

## Migration Plan

Order: (1) naming and collision architecture spec written first (failing), (2) rename per group in the order Health, Config, Sensor, Model, Consensus, `GetSnapshot` mappers, DTO collision, each group green, (3) docs and specs text, (4) golden-master file renames, (5) spec passes.
