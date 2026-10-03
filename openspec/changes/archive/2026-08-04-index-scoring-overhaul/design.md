## Context

The index enrichment feature computes 8 activity scores, 2 degree-day metrics (HDD/CDD), frost protection, and VPD from consensus forecast data. All scoring thresholds and curve parameters are hardcoded in `IndexScorer` — the only configurable values are `HeatingBaseTemp`, `CoolingBaseTemp`, and `IndoorTemp` on `IndexOptions`. The Outdoor score has a known defect: it ignores humidity and treats windstill conditions as ideal, producing misleadingly high scores on hot, humid, windless days.

Current flow:

```
IndexOptions (3 values)
        ↓
IndexResult.Compute(ConsensusSnapshot, ResolvedParameterSet, TimeProvider, IndexOptions)
        ↓
IndexScorer.OutdoorScore(temp, rainProb, wind, cloud)  ← no humidity, no preferences
IndexScorer.RunningComfort(temp, humidity, wind, rain)  ← hardcoded 5–20°C band
...etc
        ↓
IndexResult record → StatePayloadBuilder → MQTT
```

Locations are defined in `NjordOptions.Locations` as `LocationOptions` (Name, Lat, Lng, Models). The index config currently has no per-location awareness.

## Goals / Non-Goals

**Goals:**
- Remove HDD/CDD from index scoring (they are building-energy concepts, not lifestyle indices)
- Fix the Outdoor score to penalize hot+humid+windless conditions
- Introduce a cascading preference system: global → per-score → per-location, so users can tune index sensitivity without touching code
- Keep all defaults at current behavior (sensitivity = 1.0) so existing users see no change unless they configure overrides

**Non-Goals:**
- Moving HDD/CDD into the Energy enrichment feature
- Adding new index types
- Per-hour or multi-day index scores
- Changing envelope computation logic
- Changing Open-Meteo query parameters

## Decisions

### Decision 1: Preference resolution as a pre-computed lookup

**Choice:** Resolve the full cascade at config-load time into `IReadOnlyDictionary<(string Location, string Score), ResolvedPreferences>`, not at scoring time.

**Why:** `IndexScorer` is a pure static class with simple numeric inputs. Injecting cascade logic into it would mix concerns. Pre-resolving means `IndexScorer` receives flat `ResolvedPreferences` values and stays testable without config dependencies. The resolution runs once per config change, not per poll cycle.

**Alternative considered:** Pass `IndexOptions` into each scorer and resolve inside. Rejected: mixes config plumbing with math, harder to test, repeated work per cycle.

### Decision 2: Sensitivity multipliers scale the penalty term, not the weight

**Choice:** A `HeatSensitivity` of 1.5 makes `TempComfort(33°C)` = `100 - 11² × 0.3 × 1.5 = 45.6` instead of 63.7. The sub-score weights (0.35 temp + 0.25 rain + ...) stay fixed.

**Why:** Changing weights shifts the relative importance of parameters against each other, which is unintuitive ("I set HeatSensitivity to 2 and now rain matters less?"). Scaling the penalty preserves the balance between parameters but makes the user more or less sensitive to deviations from ideal.

**Alternative considered:** Expose weights directly. Rejected: too many knobs, easy to create nonsensical configs (weights that don't sum to 1.0), and users don't think in terms of weight allocation.

### Decision 3: Two-type preference model

**Choice:** Separate sensitivity multipliers (universal, cascade across all scores) from score-specific parameters (only meaningful for one score type).

```
SensitivityPreferences          ScoreParameters
├─ HeatSensitivity: 1.0        ├─ Outdoor.IdealTemp: 22.0
├─ HumiditySensitivity: 1.0    ├─ Running.IdealTempLow: 5.0
├─ WindSensitivity: 1.0        ├─ Running.IdealTempHigh: 20.0
└─ RainSensitivity: 1.0        ├─ Bbq.MinTemp: 10.0
                                ├─ Bbq.IdealWindLow: 1.0
                                ├─ Bbq.IdealWindHigh: 3.0
                                └─ Ventilation.IndoorTemp: 22.0
```

Both types are location-overridable. Sensitivities additionally cascade through the score level (global → score → location.global → location.score). Score parameters only cascade global → location (since they're already score-specific by nature).

**Why:** A single flat bag of "preferences" would force every scorer to ignore keys it doesn't understand. Typed separation makes the config self-documenting and validation straightforward.

### Decision 4: Outdoor score gets humidity + bell-curve wind

**Choice:** Add humidity as a 5th factor and replace the linear wind scoring with a bell-curve centered at 3 m/s (ideal breeze).

New Outdoor formula:
```
0.30 × TempComfort(temp, idealTemp, heatSens)
0.20 × HumidityScore(humidity, humiditySens)
0.20 × RainScore(rainProb, rainSens)
0.15 × BreezeScore(wind, windSens)    ← bell-curve, peak at 2–4 m/s
0.15 × CloudScore(cloud)
```

Weight redistribution: temp drops from 0.35→0.30, wind from 0.20→0.15, cloud from 0.20→0.15 to make room for humidity at 0.20. Rain stays at 0.20.

**Why:** Humidity is the missing factor that makes the score wrong on schwül days. The breeze curve captures that light wind provides cooling relief (especially combined with heat+humidity), while both windstill and gale are bad for outdoor comfort.

### Decision 5: Config shape in appsettings

```json
{
  "Njord": {
    "Enrichment": {
      "Indices": {
        "Enabled": true,
        "Preferences": {
          "IdealOutdoorTemp": 22.0,
          "RunningIdealTempLow": 5.0,
          "RunningIdealTempHigh": 20.0,
          "BbqMinTemp": 10.0,
          "BbqIdealWindLow": 1.0,
          "BbqIdealWindHigh": 3.0,
          "IndoorTemp": 22.0,
          "HeatSensitivity": 1.0,
          "HumiditySensitivity": 1.0,
          "WindSensitivity": 1.0,
          "RainSensitivity": 1.0
        },
        "ScoreOverrides": {
          "Running": {
            "HeatSensitivity": 0.7
          },
          "Bbq": {
            "MinTemp": 15.0,
            "RainSensitivity": 2.0
          }
        },
        "LocationOverrides": [
          {
            "Location": "Lucerne",
            "Preferences": {
              "IdealOutdoorTemp": 24.0
            },
            "ScoreOverrides": {
              "Outdoor": {
                "WindSensitivity": 0.8
              }
            }
          }
        ]
      }
    }
  }
}
```

**Why:** Flat `Preferences` (not nested by score) keeps the common case simple — most users just tweak a few global values. `ScoreOverrides` is a dictionary keyed by score name for the power-user case. `LocationOverrides` is a list (matching `NjordOptions.Locations` pattern) with its own `Preferences` + `ScoreOverrides` for full cascade depth. Location names are matched case-insensitively against configured location names.

### Decision 6: Resolution cascade order

For any (Location, Score, Property):

```
1. LocationOverrides[location].ScoreOverrides[score].{Property}
2. LocationOverrides[location].Preferences.{Property}
3. ScoreOverrides[score].{Property}
4. Preferences.{Property}
5. Hardcoded default
```

Score-specific parameters (e.g. `BbqMinTemp`) skip steps that don't apply (they're only meaningful for their score, so step 3 would never set `BbqMinTemp` on a Running override). Sensitivity multipliers flow through all 5 levels.

### Decision 7: ResolvedPreferences as scorer input

```csharp
public sealed record ResolvedPreferences(
    double IdealTemp,        // peak of comfort curve (22.0)
    double IdealTempLow,     // running/cycling lower bound (5.0)
    double IdealTempHigh,    // running/cycling upper bound (20.0)
    double MinTemp,          // bbq minimum (10.0)
    double IdealWindLow,     // bbq/breeze lower bound (1.0)
    double IdealWindHigh,    // bbq/breeze upper bound (3.0/4.0)
    double IndoorTemp,       // ventilation reference (22.0)
    double HeatSensitivity,
    double HumiditySensitivity,
    double WindSensitivity,
    double RainSensitivity);
```

Every scorer method receives the full record. Unused fields are simply ignored — this avoids needing per-score preference subtypes while keeping the API uniform.

## Risks / Trade-offs

- **Outdoor score weight changes alter existing scores** → Mitigated: only affects users with Indices enabled (off by default). Document in release notes.
- **Config complexity** → Mitigated: zero config = identical behavior to today. Only users who want tuning need to touch overrides.
- **Location name matching** → Case-insensitive string match against `LocationOptions.Name`. If a location override names a location not in `Locations`, config validation logs a warning but doesn't fail (defensive — location list may change at runtime via config mutation).
- **Wire format break (HDD/CDD removal)** → Pre-release, no stability guarantee. Persistence DTO version bump handles deserialization of old snapshots (nullable fields read as default).

## Open Questions

None — all decisions are informed by the exploration session.
