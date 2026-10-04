"""Button platform for njord."""

from __future__ import annotations

from datetime import UTC, datetime

from homeassistant.components.button import ButtonEntity
from homeassistant.config_entries import ConfigEntry
from homeassistant.core import HomeAssistant
from homeassistant.helpers.entity_platform import AddEntitiesCallback

from .const import DOMAIN
from .coordinator import NjordStatusCoordinator
from .grpc_client import NjordClient
from .helpers import server_device_info


async def async_setup_entry(
    hass: HomeAssistant,
    entry: ConfigEntry,
    async_add_entities: AddEntitiesCallback,
) -> None:
    """Set up njord button entities."""
    client: NjordClient = hass.data[DOMAIN][entry.entry_id]["client"]
    status_coordinator: NjordStatusCoordinator | None = hass.data[DOMAIN][entry.entry_id].get("status_coordinator")
    sw_version: str | None = None
    if status_coordinator is not None and status_coordinator.data is not None:
        sw_version = status_coordinator.data.version or None
    async_add_entities([NjordTriggerPollButton(entry, client, sw_version)])


class NjordTriggerPollButton(ButtonEntity):
    """Button to trigger a forecast poll on njord."""

    _attr_has_entity_name = True
    _attr_name = "Trigger Poll"

    def __init__(self, entry: ConfigEntry, client: NjordClient, sw_version: str | None = None) -> None:
        self._client = client
        self._attr_unique_id = f"{entry.entry_id}_trigger_poll"
        self._attr_device_info = server_device_info(entry, sw_version)
        self._triggered_count: int | None = None
        self._last_triggered: datetime | None = None

    async def async_press(self) -> None:
        self._triggered_count = await self._client.trigger_poll()
        self._last_triggered = datetime.now(UTC)
        self.async_write_ha_state()

    @property
    def extra_state_attributes(self) -> dict[str, object] | None:
        attrs: dict[str, object] = {}
        if self._triggered_count is not None:
            attrs["triggered_count"] = self._triggered_count
        if self._last_triggered is not None:
            attrs["last_triggered"] = self._last_triggered.isoformat()
        return attrs or None
