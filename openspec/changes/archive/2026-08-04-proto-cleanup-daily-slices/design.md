## Context

The gRPC v2 proto definitions in `protos/njord/v2/` were designed with a flat `IndexUpdate` message reflecting the old single-score `IndexResult`. The domain now produces `IndexResult` with a `Days` list of `DayScoreSet` entries (d0/d1/d2), each carrying 8 scores + envelopes + `hours_included`. The proto mapper currently squashes this to d0-only with a `Ventilation` field that no longer exists in the domain.

Additionally, proto files carry 6 `reserved` statements and 3 comments from the energy feature removal — dead weight in a pre-release project with no deployed clients.

## Goals / Non-Goals

**Goals:**
- Proto wire format matches the domain model 1:1 for index data
- All reserved fields, numbers, and energy-removal comments removed
- Envelopes exposed via gRPC (currently missing from the API entirely)
- Clean field numbering across all affected messages

**Non-Goals:**
- No changes to non-index enrichment messages
- No new RPCs
- No backwards compatibility (pre-release, no deployed clients)

## Decisions

### D1: IndexUpdate uses repeated DayScoreSet instead of flat fields

The `IndexUpdate` message wraps scores in a `repeated DayScoreSet days` field. Frost and VPD are top-level on `IndexUpdate` since they are computed once (not per-day).

**Why not keep flat + add day fields**: A flat message with `laundry_d0`, `laundry_d1` etc. would require 8×3=24 score fields + 8×3×3=72 envelope fields — unmanageable. `repeated` is the natural proto pattern for a list.

### D2: Extract FrostInfo and VpdInfo as sub-messages

Instead of optional scalar fields (`frost_hours`, `frost_confidence`, `vpd_kpa`, `vpd_category`) scattered on IndexUpdate, group them into `FrostInfo` and `VpdInfo` messages. This makes null-semantics clean: no frost → field absent; frost → complete message.

### D3: ScoreEnvelope as reusable sub-message

Each score in `DayScoreSet` can have a `ScoreEnvelope` (min/max/confidence). Rather than 3 fields per score (24 fields), a `ScoreEnvelope` message keeps it to 8 optional envelope fields.

### D4: Field renumbering — clean restart

Since this is a breaking change on a pre-release API with no deployed clients, all messages get clean sequential field numbers starting from 1. No gaps, no reserved fields.

### D5: IndexConfig removes heating/cooling base temp fields

`IndexConfig` in `admin.proto` currently has `reserved 2, 3; reserved "heating_base_temp", "cooling_base_temp"`. These fields were for HDD/CDD which was removed. The reserved statements are deleted and remaining fields renumbered.

## Risks / Trade-offs

- **Generated code churn** → All C# code in `obj/` for `Njord.Grpc.V2` regenerates. This is expected and harmless — only `EnrichmentProtoMapper` and tests reference the generated types directly.
- **Mapper complexity** → `MapIndices` becomes a loop over `Days` instead of flat field assignment. Slightly more code but structurally simpler since it mirrors the domain.
