"""Binary sensor platform for njord derived metrics."""

from __future__ import annotations

from homeassistant.components.binary_sensor import (
    BinarySensorDeviceClass,
    BinarySensorEntity,
)
from homeassistant.config_entries import ConfigEntry
from homeassistant.core import HomeAssistant
from homeassistant.helpers.entity import EntityCategory
from homeassistant.helpers.entity_platform import AddEntitiesCallback
from homeassistant.helpers.update_coordinator import CoordinatorEntity

from .const import DOMAIN
from .coordinator import NjordDataCoordinator, NjordStatusCoordinator
from .helpers import device_info, server_device_info
from .models import NjordLocation


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
    """Set up njord binary sensor entities."""
    coordinator: NjordDataCoordinator = hass.data[DOMAIN][entry.entry_id]["coordinator"]
    sw_version = _get_sw_version(hass, entry)
    disabled_groups: list[str] = entry.options.get("disabled_enrichment_groups", [])

    entities: list[BinarySensorEntity] = []
    active = coordinator.data.active_enrichments

    if (active is None or "derived" in active) and "derived" not in disabled_groups:
        locations = {loc for loc, _ in coordinator.data.forecasts}
        for location in sorted(locations):
            enrichment = coordinator.data.enrichments.get(location)
            if enrichment and enrichment.derived is not None:
                entities.append(NjordInversionEntity(coordinator, entry, location, sw_version))

    entities.append(NjordStreamSensor(coordinator, entry, "forecast", sw_version))
    entities.append(NjordStreamSensor(coordinator, entry, "enrichment", sw_version))
    entities.append(NjordStreamSensor(coordinator, entry, "config", sw_version))

    async_add_entities(entities)

    def binary_sensor_factory(location: NjordLocation) -> list[BinarySensorEntity]:
        new_entities: list[BinarySensorEntity] = []
        act = coordinator.data.active_enrichments
        if (act is None or "derived" in act) and "derived" not in disabled_groups:
            enrichment = coordinator.data.enrichments.get(location.name)
            if enrichment and enrichment.derived is not None:
                new_entities.append(NjordInversionEntity(coordinator, entry, location.name, sw_version))
        return new_entities

    coordinator.register_entity_factory("binary_sensor", async_add_entities, binary_sensor_factory)


class NjordInversionEntity(CoordinatorEntity[NjordDataCoordinator], BinarySensorEntity):
    """Binary sensor for temperature inversion detection."""

    _attr_has_entity_name = True
    _attr_entity_registry_enabled_default = False
    _attr_icon = "mdi:arrow-collapse-vertical"
    _attr_translation_key = "inversion"

    def __init__(
        self,
        coordinator: NjordDataCoordinator,
        entry: ConfigEntry,
        location: str,
        sw_version: str | None = None,
    ) -> None:
        super().__init__(coordinator)
        self._location = location

        slug = f"{location}_inversion".replace("-", "_").replace(" ", "_").lower()
        self._attr_unique_id = f"{entry.entry_id}_{slug}"
        self._attr_name = "Inversion"
        self._attr_device_info = device_info(entry, location, sw_version)

    @property
    def available(self) -> bool:
        if self.coordinator.data is None:
            return False
        enrichment = self.coordinator.data.enrichments.get(self._location)
        return enrichment is not None and enrichment.derived is not None

    @property
    def is_on(self) -> bool | None:
        if self.coordinator.data is None:
            return None
        enrichment = self.coordinator.data.enrichments.get(self._location)
        if enrichment is None or enrichment.derived is None:
            return None
        return enrichment.derived.inversion


class NjordStreamSensor(CoordinatorEntity[NjordDataCoordinator], BinarySensorEntity):
    """Binary sensor for gRPC stream connection state."""

    _attr_has_entity_name = True
    _attr_device_class = BinarySensorDeviceClass.CONNECTIVITY
    _attr_entity_category = EntityCategory.DIAGNOSTIC

    _STREAM_NAMES = {
        "forecast": "Forecast Stream",
        "enrichment": "Enrichment Stream",
        "config": "Config Stream",
    }

    def __init__(
        self,
        coordinator: NjordDataCoordinator,
        entry: ConfigEntry,
        stream_name: str,
        sw_version: str | None = None,
    ) -> None:
        super().__init__(coordinator)
        self._stream_name = stream_name
        self._attr_unique_id = f"{entry.entry_id}_{stream_name}_stream"
        self._attr_name = self._STREAM_NAMES.get(stream_name, f"{stream_name} Stream")
        self._attr_translation_key = f"{stream_name}_stream"
        self._attr_device_info = server_device_info(entry, sw_version)

    @property
    def is_on(self) -> bool:
        return self.coordinator.stream_states.get(self._stream_name, False)
