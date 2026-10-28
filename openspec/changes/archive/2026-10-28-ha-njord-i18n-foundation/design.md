## Context

ha-njord has ~54 user-facing entities. Only 8 use HA's `translation_key` system (config flow, repairs, event types, 3 diagnostics). The rest hardcode English names via `self._attr_name`. Enum states (safe/caution/dangerous) are raw wire strings. `translations/en.json` does not exist (required for custom integrations).

Three entities already set `_attr_translation_key` but also set `_attr_name` which overrides it: `NjordStreamSensor`, `NjordUptimeSensor`, `NjordTargetSensor`.

`ScoreData.best_start`/`best_hours` are parsed from gRPC into `FeatureData.score` but `_attributes()` only iterates `item.values`, never `item.score`.

## Goals / Non-Goals

**Goals:**
- Every user-facing entity uses `translation_key` instead of hardcoded names
- All enum states declared in strings.json for HA auto-translation
- Complete `translations/en.json` (required runtime file for custom integrations)
- Complete `translations/de.json` with German translations
- Expose `best_start`/`best_hours` as sensor attributes
- Establish the pattern all future features follow

**Non-Goals:**
- New entity types, new features, proto changes
- Attribute name/value translations (HA frontend support limited to climate)
- Languages beyond EN/DE (community contributions later)

## Decisions

### 1. Use `key` as translation_key (not a separate field)

Every description dataclass already has a `key: str` field that's unique per entity. Use it directly as the `translation_key` value rather than adding a new field. For alerts, derive from `AlertType.value` (e.g., `"frost"` → translation_key `"frost_alert"`).

**Why:** Less churn, `key` is already the natural identifier. AlertMeta keeps its `name` for potential internal use but entities ignore it for display.

### 2. Create translations/en.json alongside strings.json

Custom integrations must have `translations/en.json` with fully expanded strings (no `[%key:...]` references). HA does not resolve references for custom integrations. Keep `strings.json` as the source for HACS validation and generate/maintain `en.json` as a flat copy.

**Why:** Official HA requirement for custom integrations. Without it, translations only work because of HACS fallback behavior which may break.

### 3. Remove _attr_name, don't add _attr_translation_key to dataclass

Remove `self._attr_name = ...` from entity constructors. Set `self._attr_translation_key` directly from the description key or alert type. Don't add a `translation_key` field to the dataclasses — derive it in the entity.

**Why:** Minimal change to dataclass contracts. The entity constructor is the right place for HA integration details.

### 4. BestStart/BestHours as optional attributes

Add `best_start` and `best_hours` to `extra_state_attributes` only when `item.score` is not None and the values are present. Use ISO format for `best_start`.

**Why:** Not all lifestyle sensors have scores. Optional attributes avoid noise on non-score entities.

## Risks / Trade-offs

- **Entity ID stability**: Low risk. HA uses `unique_id` for identity. Switching to `translation_key` only changes display name. Existing automations using `entity_id` are unaffected.
- **Missing translation falls back to EN**: If `de.json` misses an entry, HA falls back to `en.json`. Users never see raw keys.
- **HACS validation**: `hassfest` validates translations. Running it after changes catches mismatches early.
