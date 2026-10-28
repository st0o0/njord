# ha-njord i18n Foundation

## Summary

Refactor ha-njord to use Home Assistant's `translation_key` pattern for all user-facing entities. Replace every hardcoded English `self._attr_name` with `self._attr_translation_key`, declare all enum states in `strings.json`, complete `translations/de.json` with German translations, and expose the already-parsed `BestStart`/`BestHours` score fields as entity attributes.

## Motivation

ha-njord currently has ~50 user-facing entities with hardcoded English names. Only 8 entities (config flow, repair issues, event types, 3 diagnostics) use HA's proper `translation_key` system. This means:

- German-speaking users see "Frost Alert" instead of "Frostwarnung"
- Enum states like `safe` / `caution` / `dangerous` appear as raw wire strings instead of localized labels
- Alert severities `yellow` / `orange` / `red` are untranslated
- No community translations are possible because the strings aren't in the translation files
- `ScoreData.best_start` and `ScoreData.best_hours` are parsed from gRPC but never surfaced as entity attributes -- invisible data

AccuWeather is the HA gold reference for weather i18n: every sensor uses `translation_key`, every enum state is declared in `strings.json`, and `state_attributes` translations cover attribute values. ha-njord should follow the same pattern before adding new features.

## Scope

**Repository:** ha-njord (D:\GIT\ha-njord) only. No changes to njord, no proto changes, no new features.

### Entities to migrate (12 `self._attr_name` assignments)

**Alert binary sensors (20 entities, `binary_sensor.py`):**
- `ALERT_META` dict provides `name` → replace with `translation_key` derived from `AlertType` value
- Frost Alert, Heat Alert, Storm Alert, Heavy Rain Alert, UV Alert, Fog Alert, Snow Alert, Pressure Drop Alert, Thunderstorm Alert, Ice Alert, Wind Chill Alert, Visibility Alert, Tropical Night Alert, Humidity Alert, Pollen Alert, PM2.5 Alert, Dust Alert, Flood Risk Alert, Flood Rain Alert, Swimming Conditions Alert

**Domain sensors (12 entities, `sensor.py`):**
- `DomainSensorDescription.name` → replace with `translation_key` derived from `key`
- PM2.5, PM10, Ozone, Birch Pollen, Grass Pollen, Alder Pollen, Mugwort Pollen, River Discharge, Wave Height, Wave Period, Wave Direction, Water Temperature

**Lifestyle sensors (14 entities, `lifestyle.py`):**
- `LifestyleSensorDescription.name` → replace with `translation_key` derived from `key`
- Laundry Score, Commute Safety, Exercise, Outdoor Score, BBQ Score, Surf Conditions, Pollen Trend, Night Ventilation, Forecast Confidence Trend, Solar Yield, Flood Peak Discharge, Front Approaching, Seasonal Temperature Anomaly, Model Temperature Spread

**Lifestyle binary sensors (2 entities, `lifestyle.py`):**
- `LifestyleBinarySensorDescription.name` → replace with `translation_key`
- Ventilation Window, Planting Safe

**Diagnostic sensors (5 entities, `sensor.py`):**
- Monthly Usage, Daily Usage, Version, Uptime (partially done), Poll {location}

**Event entity (1 entity, `event.py`):**
- Weather Alert -- already has `translation_key` but also sets `self._attr_name` (redundant, remove the hardcoded name)

**Weather entity (1+ entities, `weather.py`):**
- Model display names are dynamic (from gRPC `ModelInfo`), keep as `self._attr_name` -- these are external model identifiers, not translatable labels

### Enum states to declare

| Entity | States | Current |
|--------|--------|---------|
| Commute Safety | safe, caution, dangerous | Raw wire strings |
| Exercise | safe, caution, avoid | Raw wire strings |
| Alert severity (attribute) | none, yellow, orange, red | Raw StrEnum values |
| Pollen Trend | rising, stable, falling (if enum) | Raw wire string |
| Confidence Trend direction | rising, stable, falling (if enum) | Raw wire string |

### BestStart/BestHours exposure

`FeatureData.score` contains `best_start` (Timestamp) and `best_hours` (int) parsed from gRPC `ScoreData`, but `NjordLifestyleSensor._attributes()` only iterates `item.values`, not `item.score`. Fix: include `best_start` and `best_hours` in `extra_state_attributes` for all Score-type sensors (Laundry, Outdoor, BBQ, Surf).

## Design

### Pattern: translation_key on description dataclasses

Replace `name: str` with `translation_key: str` on `AlertMeta`, `DomainSensorDescription`, `LifestyleSensorDescription`, and `LifestyleBinarySensorDescription`. In the entity constructors, set `self._attr_translation_key` and `self._attr_has_entity_name = True` instead of `self._attr_name`.

```python
# Before
self._attr_name = meta.name  # "Frost Alert"

# After
self._attr_translation_key = meta.translation_key  # "frost_alert"
self._attr_has_entity_name = True
```

### Pattern: enum state translations in strings.json

```json
{
  "entity": {
    "sensor": {
      "commute_safety": {
        "name": "Commute Safety",
        "state": {
          "safe": "Safe",
          "caution": "Caution",
          "dangerous": "Dangerous"
        }
      }
    }
  }
}
```

### strings.json structure

Following the AccuWeather pattern, `strings.json` remains the source of truth (English). All entity names, enum states, and translatable attribute states are declared here. The existing config flow, options, issues, and event translations remain unchanged.

### translations/de.json structure

Mirror of `strings.json` with German translations. Already exists with config flow, options, issues, and event translations. Extend with all entity names and enum states.

### BestStart/BestHours in attributes

In `NjordLifestyleSensor._attributes()`, after building the values dict from `item.values`, append `best_start` and `best_hours` from `item.score` when present. Attribute keys: `best_start` (ISO timestamp or None), `best_hours` (int or None).

### Entity ID stability

Entity IDs are generated from the English name on first creation and persist in HA's entity registry. Switching from `self._attr_name` to `self._attr_translation_key` does not change entity IDs for existing installations because HA uses `unique_id` for identity, not the display name. The display name updates automatically.

## Non-goals

- New entity types (calendar, additional event entities)
- New features or feature rules in njord
- Proto changes in njord
- Changes to the gRPC client or data model
- Adding new languages beyond EN and DE (community contributions welcome after this change)
- Translating calendar event summaries (future change: score-windows)
- Attribute name translations (HA frontend support is limited to climate entities)

## Dependencies

None. This is a standalone ha-njord refactor with no njord-side changes.

## Risks

- **Entity ID churn on existing installations:** Low risk. HA uses `unique_id` for entity identity. Switching from `name` to `translation_key` changes the display name only. Existing automations referencing `entity_id` (e.g., `binary_sensor.njord_berlin_frost_alert`) are unaffected because entity IDs are stable once created.
- **Missing translations show raw keys:** If a `translation_key` entry is missing from `translations/de.json`, HA falls back to `translations/en.json` (which is complete by construction). Users never see raw keys.
- **Description dataclass changes:** `AlertMeta`, `DomainSensorDescription`, `LifestyleSensorDescription` all change their `name` field to `translation_key`. Any code reading `.name` must be updated. Grep for all usages.

## API budget impact

None. This change is ha-njord only and does not affect polling or API usage.
