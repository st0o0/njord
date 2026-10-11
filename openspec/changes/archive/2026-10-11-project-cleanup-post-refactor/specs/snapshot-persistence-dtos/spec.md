## REMOVED Requirements

### Requirement: Scheduler snapshot DTO preserves poll state
**Reason**: SchedulerActor is event-sourced only — it persists `DataChangedDto`
events and explicitly ignores snapshots (`Recover<SnapshotOffer>(_ => { })`).
The snapshot DTOs, mapping methods, and serialization tests are dead code.
**Migration**: Remove `SchedulerSnapshotDto`, `ModelPollStateDto`,
`SchedulerDtoMapping.ToSnapshot`/`FromSnapshot`, `SchedulerDtoSerializationSpec`,
and `SchedulerDtoRoundtripSpec`. Keep `SchedulerDtoMapping.ToDto`/`FromDto` for
the event DTOs.
