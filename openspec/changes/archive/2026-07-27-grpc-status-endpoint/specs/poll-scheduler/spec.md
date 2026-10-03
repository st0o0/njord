## ADDED Requirements

### Requirement: SchedulerActor handles GetPollStates query in Ready state
The SchedulerActor SHALL handle `GetPollStates` messages in the `Ready` behavior
by iterating its `_states` dictionary and responding to the sender with a
`PollStatesSnapshot` containing one `PollStateEntry` per entry. The handler SHALL
NOT modify any state or trigger any side effects.

#### Scenario: Query returns all states
- **WHEN** a `GetPollStates` message is received in Ready state with 6 model poll states
- **THEN** the actor SHALL respond with `PollStatesSnapshot` containing 6 entries

#### Scenario: Query is read-only
- **WHEN** a `GetPollStates` message is received
- **THEN** no events SHALL be persisted and no timers SHALL be scheduled

### Requirement: GetPollStates is stashed in non-Ready behaviors
The SchedulerActor SHALL stash `GetPollStates` in `WaitingForRefs`, `Connecting`,
and `WaitingForConnection` behaviors alongside other stashed commands. The message
SHALL be unstashed and handled when the actor transitions to Ready.

#### Scenario: Stashed query is answered after Ready transition
- **WHEN** a `GetPollStates` is received during `Connecting` and the actor later transitions to Ready
- **THEN** the `PollStatesSnapshot` SHALL be sent to the original sender after unstashing
