# Marine Features

Marine features monitor ocean conditions using the Open-Meteo Marine API. They are relevant for coastal locations, harbors, and anyone near the sea. The Marine API may return no data for inland locations -- this is expected and the features simply stay inactive.

## Swimming

Rates swimming safety based on wave height and sea surface temperature. Designed for casual beach swimmers, not experienced open-water athletes.

| Condition | Meaning |
|-----------|---------|
| Safe | Calm seas, comfortable water temperature |
| Caution | Moderate waves or cool water |
| Dangerous | High waves or very cold water |

**Entity:** `binary_sensor.<location>_swimming_conditions_alert`
**Domains:** Marine | **Default:** Off

```yaml
Njord__Features__Marine__Swimming__Enabled: "true"
```

**Automation ideas:**
- Morning notification for beach days: "Swimming conditions: safe"
- Warn when conditions deteriorate during the day
- Display on a vacation property dashboard

---

## Surf

Reports surf conditions from swell wave height, swell period, and wind direction. Longer swell periods with moderate height and offshore (or light) wind produce the best surfing conditions.

**Entity:** `sensor.<location>_surf_conditions`
**Domains:** Weather + Marine | **Default:** Off

```yaml
Njord__Features__Marine__Surf__Enabled: "true"
```

**Automation ideas:**
- Dawn patrol alert when swell and wind align
- Compare conditions across multiple surf spots (multiple locations)
- Track swell trends for weekend planning

---

## OceanCurrents

Reports ocean current speed and direction. Useful for maritime operations, fishing, diving, and coastal safety planning.

**Entity:** `sensor.<location>_ocean_currents`
**Domains:** Marine | **Default:** Off

```yaml
Njord__Features__Marine__OceanCurrents__Enabled: "true"
Njord__Features__Marine__OceanCurrents__StrongCurrentThresholdKmh: "3.0"
```

**Automation ideas:**
- Alert divers about strong current conditions
- Factor currents into sailing route planning
- Monitor harbor approach current for vessel traffic
