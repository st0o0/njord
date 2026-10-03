## MODIFIED Requirements

### Requirement: EgressActor pre-materializes hub before vending refs
The `EgressActor` SHALL pre-materialize the MergeHub and BroadcastHub in `PreStart` so that StreamRef requests can be served immediately. The MergeHub SHALL use a per-producer buffer size of 8. The BroadcastHub SHALL use a buffer size of 16.

#### Scenario: BroadcastHub buffer size is 16
- **WHEN** the EgressActor pre-materializes its hubs
- **THEN** the BroadcastHub SHALL use a buffer size of 16
