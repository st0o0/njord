## MODIFIED Requirements

### Requirement: IndexResult includes per-model envelope for each activity score
`IndexResult` SHALL include, for each numeric score field (Laundry, Outdoor, Running, Cycling, Bbq, Irrigation, Solar, Ventilation): a `_min` (int), `_max` (int), and `_confidence` (double, 0.0–1.0) field. These represent the range across individual per-model computations and the agreement level.

#### Scenario: Score envelope fields
- **WHEN** IndexResult is computed from a snapshot with 5 models
- **THEN** the result exposes OutdoorMin, OutdoorMax, OutdoorConfidence (and likewise for all 8 scores)

#### Scenario: Tight model agreement
- **WHEN** 5 models produce outdoor scores [70, 72, 71, 73, 70] with confidence tolerance 10%
- **THEN** OutdoorConfidence=1.0, OutdoorMin=70, OutdoorMax=73

#### Scenario: Wide disagreement
- **WHEN** models produce outdoor scores [30, 50, 70, 80, 90] with confidence tolerance 10%
- **THEN** OutdoorMin=30, OutdoorMax=90, OutdoorConfidence reflects fraction within ±10% of median (70)

### Requirement: Index computation evaluates each model independently then aggregates
`IndexResult.Compute` SHALL compute a full score set per model using only that model's hourly data. The primary output values (existing) SHALL remain the mean/median-based computation. Envelope fields SHALL be derived from the per-model results: min = lowest per-model score, max = highest per-model score, confidence = fraction of models within tolerance of median score.

#### Scenario: Per-model isolation
- **WHEN** model A predicts rain (low outdoor score), model B predicts sun (high outdoor score)
- **THEN** per-model scores differ significantly, producing a wide min/max range

#### Scenario: Single model
- **WHEN** only 1 model is available
- **THEN** all _min = _max = the single score, all _confidence = 1.0

#### Scenario: Confidence tolerance
- **WHEN** tolerance is configured at 15% and median outdoor score is 60
- **THEN** models with scores between 51 and 69 count as "agreeing" for confidence

### Requirement: State payload includes envelope fields alongside existing scores
The indices state JSON SHALL include `_min`, `_max`, `_confidence` variants for each score key. Existing field names and values SHALL NOT change.

#### Scenario: JSON structure
- **WHEN** index result is serialized
- **THEN** JSON contains `{"outdoor": 72, "outdoor_min": 65, "outdoor_max": 80, "outdoor_confidence": 0.8, "running": 55, "running_min": 40, ...}`

### Requirement: Discovery registers envelope components
`IndexEnrichment.BuildDiscoveryPayload` SHALL register additional sensor components for each envelope field. They SHALL share the same state topic as the base score and use value_template to extract the specific key.

#### Scenario: Envelope discovery components
- **WHEN** discovery payload is built for indices
- **THEN** components include `outdoor_min`, `outdoor_max`, `outdoor_confidence` with appropriate value templates
