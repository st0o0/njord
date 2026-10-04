"""Event platform for njord weather alerts."""

from __future__ import annotations

from homeassistant.components.event import EventEntity
from homeassistant.config_entries import ConfigEntry
from homeassistant.core import HomeAssistant, callback
from homeassistant.helpers.entity_platform import AddEntitiesCallback
from homeassistant.helpers.update_coordinator import CoordinatorEntity

from .const import DOMAIN
from .coordinator import NjordDataCoordinator, NjordStatusCoordinator
from .helpers import device_info
from .models import EnrichmentData, NjordLocation

EVENT_ALERT_STARTED = "alert_started"
EVENT_ALERT_ESCALATED = "alert_escalated"
EVENT_ALERT_DEESCALATED = "alert_deescalated"
EVENT_ALERT_CLEARED = "alert_cleared"


def _get_sw_version(hass: HomeAssistant, entry: ConfigEntry) -> str | None:
    status_coordinator: NjordStatusCoordinator | None = hass.data[DOMAIN][entry.entry_id].get("status_coordinator")
    if status_coordinator is not None and status_coordinator.data is not None:
        return status_coordinator.data.version or None
    return None


async def async_setup_entry(
    hass: HomeAssistant,
    entry: ConfigEntry,
    async_add_entities: AddEntitiesCallback,
) -> None:
    """Set up njord event entities."""
    coordinator: NjordDataCoordinator = hass.data[DOMAIN][entry.entry_id]["coordinator"]
    sw_version = _get_sw_version(hass, entry)
    disabled_groups: list[str] = entry.options.get("disabled_enrichment_groups", [])

    entities: list[EventEntity] = []
    active = coordinator.data.active_enrichments

    if (active is None or "alerts" in active) and "alerts" not in disabled_groups:
        locations = {loc for loc, _ in coordinator.data.forecasts}
        for location in sorted(locations):
            entities.append(NjordWeatherAlertEvent(coordinator, entry, location, sw_version))

    async_add_entities(entities)

    def event_factory(location: NjordLocation) -> list[EventEntity]:
        act = coordinator.data.active_enrichments
        if act is not None and "alerts" not in act:
            return []
        if "alerts" in disabled_groups:
            return []
        return [NjordWeatherAlertEvent(coordinator, entry, location.name, sw_version)]

    coordinator.register_entity_factory("event", async_add_entities, event_factory)


class NjordWeatherAlertEvent(CoordinatorEntity[NjordDataCoordinator], EventEntity):
    """Event entity that fires on weather alert transitions."""

    _attr_has_entity_name = True
    _attr_translation_key = "weather_alert"
    _attr_icon = "mdi:weather-lightning-rainy"
    _attr_event_types = [
        EVENT_ALERT_STARTED,
        EVENT_ALERT_ESCALATED,
        EVENT_ALERT_DEESCALATED,
        EVENT_ALERT_CLEARED,
    ]

    def __init__(
        self,
        coordinator: NjordDataCoordinator,
        entry: ConfigEntry,
        location: str,
        sw_version: str | None = None,
    ) -> None:
        super().__init__(coordinator)
        self._location = location
        self._previous_alerts: dict[str, str] = self._current_alert_map()

        slug = f"{location}_weather_alert".replace("-", "_").replace(" ", "_").lower()
        self._attr_unique_id = f"{entry.entry_id}_{slug}"
        self._attr_name = "Weather Alert"
        self._attr_device_info = device_info(entry, location, sw_version)

    def _current_alert_map(self) -> dict[str, str]:
        if self.coordinator.data is None:
            return {}
        enrichment: EnrichmentData | None = self.coordinator.data.enrichments.get(self._location)
        if enrichment is None:
            return {}
        return {a.type: a.severity for a in enrichment.alerts if a.severity != "none"}

    def _alert_data(self, alert_type: str) -> dict[str, object]:
        base: dict[str, object] = {"type": alert_type, "location": self._location}
        if self.coordinator.data is None:
            return base
        enrichment = self.coordinator.data.enrichments.get(self._location)
        if enrichment is None:
            return base
        for a in enrichment.alerts:
            if a.type == alert_type:
                data: dict[str, object] = {
                    "type": a.type,
                    "location": self._location,
                    "severity": a.severity,
                    "confidence": a.confidence,
                    "trigger_value": a.trigger_value,
                    "threshold": a.threshold,
                }
                if a.peak_value is not None:
                    data["peak_value"] = a.peak_value
                if a.hours_until is not None:
                    data["hours_until"] = a.hours_until
                if a.duration_hours is not None:
                    data["duration_hours"] = a.duration_hours
                return data
        return base

    @callback
    def _handle_coordinator_update(self) -> None:
        current = self._current_alert_map()

        all_types = set(self._previous_alerts) | set(current)
        for alert_type in all_types:
            prev_severity = self._previous_alerts.get(alert_type)
            curr_severity = current.get(alert_type)

            if prev_severity == curr_severity:
                continue

            if prev_severity is None and curr_severity is not None:
                self._trigger_event(EVENT_ALERT_STARTED, self._alert_data(alert_type))
            elif prev_severity is not None and curr_severity is None:
                self._trigger_event(
                    EVENT_ALERT_CLEARED,
                    {
                        "type": alert_type,
                        "location": self._location,
                        "previous_severity": prev_severity,
                    },
                )
            elif prev_severity is not None and curr_severity is not None:
                severity_order = {"yellow": 1, "orange": 2, "red": 3}
                prev_rank = severity_order.get(prev_severity, 0)
                curr_rank = severity_order.get(curr_severity, 0)
                event_type = EVENT_ALERT_ESCALATED if curr_rank > prev_rank else EVENT_ALERT_DEESCALATED
                data = self._alert_data(alert_type)
                data["previous_severity"] = prev_severity
                self._trigger_event(event_type, data)

        self._previous_alerts = current
        self.async_write_ha_state()
