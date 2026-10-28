# Tasks: ha-njord-i18n-foundation

All work in `D:\GIT\ha-njord`, directory `custom_components/njord/`.

## 1. Fix translation_key override bugs

Three entities already set `_attr_translation_key` but also set `_attr_name` which overrides it.

- [x] `binary_sensor.py` `NjordStreamSensor`: remove `self._attr_name = self._STREAM_NAMES.get(...)` (line ~212). The `_attr_translation_key` on line ~213 is already correct.
- [x] `sensor.py` `NjordUptimeSensor`: remove `self._attr_name = "Uptime"` (line ~484). The `_attr_translation_key = "uptime"` on line ~478 is already correct.
- [x] `sensor.py` `NjordTargetSensor`: remove `self._attr_name = f"Poll {location}"` (line ~517). The `_attr_translation_key = "target_poll"` on line ~518 is already correct. Ensure `translation_placeholders` includes `location`.
- [x] `event.py` `NjordAlertEvent`: remove `self._attr_name = "Weather Alert"` (line ~77). The `_attr_translation_key = "weather_alert"` on line ~59 is already correct.

## 2. Migrate alert binary sensors to translation_key

- [x] In `binary_sensor.py` `NjordAlertBinarySensor.__init__`: replace `self._attr_name = meta.name` with `self._attr_translation_key = f"{alert_type.value}_alert"`. Do not set `_attr_name`. `_attr_has_entity_name` is already True.
- [x] `AlertMeta` in `const.py`: keep `name` field for now (used as fallback reference) but entities no longer read it for display.

## 3. Migrate domain sensors to translation_key

- [x] In `sensor.py` `NjordDomainSensor.__init__`: replace `self._attr_name = description.name` with `self._attr_translation_key = description.key`. Do not set `_attr_name`.

## 4. Migrate lifestyle sensors to translation_key

- [x] In `sensor.py` `NjordLifestyleSensor.__init__`: replace `self._attr_name = description.name` with `self._attr_translation_key = description.key`. Do not set `_attr_name`.
- [x] In `binary_sensor.py` `NjordLifestyleBinarySensor.__init__`: replace `self._attr_name = description.name` with `self._attr_translation_key = description.key`. Do not set `_attr_name`.

## 5. Migrate diagnostic sensors to translation_key

- [x] `NjordMonthlyUsageSensor`: replace `self._attr_name = "Monthly Usage"` with `self._attr_translation_key = "monthly_usage"`. Set `_attr_has_entity_name = True`.
- [x] `NjordDailyUsageSensor`: replace `self._attr_name = "Daily Usage"` with `self._attr_translation_key = "daily_usage"`. Set `_attr_has_entity_name = True`.
- [x] `NjordVersionSensor`: replace `self._attr_name = "Version"` with `self._attr_translation_key = "version"`. Set `_attr_has_entity_name = True`.

## 6. Expose BestStart/BestHours as sensor attributes

- [x] In `sensor.py` `_attributes()` function (or `NjordLifestyleSensor.extra_state_attributes`): after building the values dict from `item.values`, check if `item.score` is not None. If so, add `best_start` (ISO format or None) and `best_hours` (int or None) to the attributes dict.

## 7. Create complete strings.json entity section

- [x] Add `entity` block to `strings.json` with all entity translations as specified in the entity-translations spec. Include:
  - `binary_sensor`: all 20+ alert translation_keys + 2 lifestyle binary sensors + stream sensors (already there)
  - `sensor`: all 12 domain sensors + 14 lifestyle sensors + 5 diagnostic sensors (partially there)
  - `event`: weather_alert (already there, verify completeness)
  - Enum states for `commute_safety` and `exercise` under their `state` blocks

## 8. Create translations/en.json

- [x] Create `translations/en.json` as a complete flat copy of `strings.json` with all `[%key:...]` references resolved to their actual English text. This is required for custom integrations — HA does not resolve references at runtime for custom components.

## 9. Complete translations/de.json

- [x] Extend `translations/de.json` with all entity translations in German, matching the structure of `strings.json`/`en.json`. Use the German names from the entity-translations spec.

## Validation

```bash
# From D:\GIT\ha-njord
# Verify no remaining self._attr_name in entity files (except weather.py model names)
grep -rn "self._attr_name" custom_components/njord/ --include="*.py"

# Verify all translation_keys have entries in strings.json and de.json
# Manual review: compare translation_keys in Python against keys in JSON
```
