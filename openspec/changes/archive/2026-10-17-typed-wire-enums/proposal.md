## Why

Most closed sets in njord are already C# enums, but a handful of places still derive wire or metric strings from enum member names (`ToString()`, `Enum.TryParse`, a ternary) or keep a free `string` where the set is closed (VPD category). A renamed member then silently changes the gRPC, MQTT or Prometheus contract, and a new `PollPhase` would silently map to `"discovery"`. The gRPC fields that carry these sets are `string`, so clients (ha-njord) match on text. njord and ha-njord ship together as 0.5.0, which is the cheapest moment for a wire break.

## What Changes

- **Domain**
  - New `VpdCategory` enum (`Low`, `Optimal`, `High`, `Critical`) with `ToWireName()` in `Njord.Compute/Contracts`; `VpdInfo.Category` becomes `VpdCategory`.
  - `PollPhase` gets an exhaustive mapping with a throwing default instead of the ternary in `OpsProtoMappings`.
  - `ConsensusMethod` gets an explicit wire mapping (no `Enum.TryParse` / `ToString()`).
  - `AlertSeverity.ToWireName()` replaces `ToString().ToLowerInvariant()` in the MQTT `StatePayloadBuilder`.
  - The fetch-outcome metric tag in `OpenMeteoClient` comes from an explicit switch.
  - Named constants for the sensor source `"default"` and the MQTT `"unknown"` location fallback.
- **BREAKING (proto)**: the closed-set `string` fields become proto enums, each with `*_UNSPECIFIED = 0`. The old field number is `reserved` (the name is kept by the new enum field, protobuf does not allow a reserved name to be reused), the enum field gets a new number:
  - `ModelStatus.phase`, `TriggerTarget.phase` → `PollPhase`
  - `ParameterTrend.direction` → `TrendDirection`
  - `stability_label` → `Stability`
  - `HorizonDerived.dewpoint_comfort` → `DewPointComfort`
  - `VpdInfo.category` → `VpdCategory`
  - `ConsensusConfig.method` → `ConsensusMethod` (`MEAN`, `MEDIAN`, `TRIMMED_MEAN`; was PascalCase text)
- Update the gRPC mappings, golden masters, `docs/` pages that show these values, and the affected main specs.

## Non-goals

- `horizon`, `parameter`, `unit`, `source`, `models`, `rejection_reason` stay strings (open or parameterised sets).
- `EnrichmentEvent.type_name` stays a string in this change (see design).
- No enum for sensor rejection reasons.
- No persistence change and no polling change (no API-budget impact: zero additional requests).
- MQTT payload strings and Prometheus label values stay byte-identical.

## Capabilities

### New Capabilities

- `grpc-wire-enums`: the closed sets exposed over gRPC are proto enums with an `UNSPECIFIED` zero value, mapped exhaustively, with clients expected to tolerate unknown values.

### Modified Capabilities

- `grpc-v2-common`: `VpdInfo.category` is the `VpdCategory` enum.
- `grpc-v2-ops-service`: `TriggerTarget.phase` (and `ModelStatus.phase`) is the `PollPhase` enum.

## Impact

- `protos/njord/v2/{common,ops,admin}.proto` and regenerated C# stubs.
- `Njord.Compute` (`VpdInfo`, `VpdCategory`), `Njord.Indices/VaporPressureDeficit.cs`, `Njord.Grpc/Mapping/{Analysis,Ops,Config}ProtoMappings.cs`, `Njord.Mqtt/StatePayloadBuilder.cs`, `Njord.OpenMeteo/OpenMeteoClient.cs`, `Njord.Grpc/Mapping/SensorProtoMappings.cs`, `Njord.Mqtt/MqttStatePublisher.cs`.
- Tests: `Njord.Grpc.Tests` (golden masters), `Njord.Indices.Tests`, `Njord.Mqtt.Tests`, `Njord.OpenMeteo.Tests`, `Njord.Compute.Tests`.
- Consumer: ha-njord must sync the protos and map the enums (its own change `typed-enums`); both release as 0.5.0.
