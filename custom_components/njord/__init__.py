"""njord Weather integration for Home Assistant."""

from __future__ import annotations

import logging
from datetime import timedelta

import voluptuous as vol
from homeassistant.config_entries import ConfigEntry
from homeassistant.const import Platform
from homeassistant.core import Event, HomeAssistant, ServiceCall
from homeassistant.core import callback as ha_callback
from homeassistant.exceptions import ServiceValidationError
from homeassistant.helpers.event import async_track_state_change_event

from .const import DOMAIN
from .coordinator import NjordDataCoordinator, NjordStatusCoordinator
from .grpc_client import SENSOR_KIND_MAP, NjordClient

_LOGGER = logging.getLogger(__name__)

PLATFORMS = [Platform.WEATHER, Platform.BINARY_SENSOR, Platform.SENSOR, Platform.BUTTON, Platform.EVENT]

SERVICE_TRIGGER_POLL = "trigger_poll"
SERVICE_TRIGGER_POLL_SCHEMA = vol.Schema(
    {
        vol.Optional("location", default=""): str,
        vol.Optional("model", default=""): str,
    }
)

SERVICE_PUSH_SENSOR = "push_sensor"
SERVICE_PUSH_SENSOR_SCHEMA = vol.Schema(
    {
        vol.Required("kind"): str,
        vol.Required("entity_id"): str,
        vol.Optional("location", default=""): str,
        vol.Optional("source", default=""): str,
    }
)

DEFAULT_STATUS_POLL_INTERVAL = 30


async def async_setup_entry(hass: HomeAssistant, entry: ConfigEntry) -> bool:
    """Set up njord from a config entry."""
    client = NjordClient(
        host=entry.data["host"],
        port=entry.data["port"],
    )
    await client.connect()

    coordinator = NjordDataCoordinator(hass, client)
    await coordinator.async_config_entry_first_refresh()

    poll_interval = entry.options.get("status_poll_interval", DEFAULT_STATUS_POLL_INTERVAL)
    status_coordinator = NjordStatusCoordinator(hass, client, poll_interval=poll_interval)
    try:
        await status_coordinator.async_config_entry_first_refresh()
    except Exception:
        status_coordinator = None

    if status_coordinator is not None:
        coordinator.data.active_enrichments = set(status_coordinator.data.active_enrichments)

    hass.data.setdefault(DOMAIN, {})
    hass.data[DOMAIN][entry.entry_id] = {
        "client": client,
        "coordinator": coordinator,
        "status_coordinator": status_coordinator,
    }

    await hass.config_entries.async_forward_entry_setups(entry, PLATFORMS)

    coordinator.start_streams()

    if status_coordinator is not None:
        _previous_enrichments: frozenset[str] = frozenset(status_coordinator.data.active_enrichments)

        @ha_callback
        def _check_enrichment_changes() -> None:
            nonlocal _previous_enrichments
            if status_coordinator.data is None:
                return
            current = frozenset(status_coordinator.data.active_enrichments)
            if current != _previous_enrichments:
                _previous_enrichments = current
                hass.async_create_task(hass.config_entries.async_reload(entry.entry_id))

        entry.async_on_unload(status_coordinator.async_add_listener(_check_enrichment_changes))

    async def _async_options_updated(hass: HomeAssistant, config_entry: ConfigEntry) -> None:
        new_interval = config_entry.options.get("status_poll_interval", DEFAULT_STATUS_POLL_INTERVAL)
        if status_coordinator is not None:
            status_coordinator.update_interval = timedelta(seconds=new_interval)

    entry.async_on_unload(entry.add_update_listener(_async_options_updated))

    sensor_push_config = entry.options.get("sensor_push", {})
    reverse_map: dict[str, tuple[str, str]] = {}
    for location, kinds in sensor_push_config.items():
        for kind, entity_ids in kinds.items():
            for entity_id in entity_ids:
                reverse_map[entity_id] = (location, kind)

    if reverse_map:

        @ha_callback
        def _sensor_state_changed(event: Event) -> None:
            new_state = event.data.get("new_state")
            if new_state is None:
                return
            entity_id = new_state.entity_id
            mapping = reverse_map.get(entity_id)
            if mapping is None:
                return
            try:
                value = float(new_state.state)
            except (ValueError, TypeError):
                return
            location, kind = mapping
            hass.async_create_task(_async_push_sensor(client, kind, location, value, entity_id))

        unsub = async_track_state_change_event(hass, list(reverse_map.keys()), _sensor_state_changed)
        entry.async_on_unload(unsub)

    if not hass.services.has_service(DOMAIN, SERVICE_TRIGGER_POLL):

        async def handle_trigger_poll(call: ServiceCall) -> None:
            location = call.data.get("location", "")
            model = call.data.get("model", "")
            for entry_data in hass.data[DOMAIN].values():
                c: NjordClient = entry_data["client"]
                await c.trigger_poll(location=location, model=model)

        hass.services.async_register(
            DOMAIN,
            SERVICE_TRIGGER_POLL,
            handle_trigger_poll,
            schema=SERVICE_TRIGGER_POLL_SCHEMA,
        )

    if not hass.services.has_service(DOMAIN, SERVICE_PUSH_SENSOR):

        async def handle_push_sensor(call: ServiceCall) -> None:
            kind = call.data["kind"]
            entity_id = call.data["entity_id"]
            location = call.data.get("location", "")
            source = call.data.get("source", "") or entity_id

            if kind not in SENSOR_KIND_MAP:
                raise ServiceValidationError(f"Unknown sensor kind: {kind!r}")

            state = hass.states.get(entity_id)
            if state is None:
                raise ServiceValidationError(f"Entity not found: {entity_id}")

            try:
                value = float(state.state)
            except (ValueError, TypeError):
                raise ServiceValidationError(f"Entity {entity_id} state is not numeric: {state.state!r}")

            if not location:
                locations = set()
                for entry_data in hass.data[DOMAIN].values():
                    coord = entry_data.get("coordinator")
                    if coord is not None and hasattr(coord, "_known_locations"):
                        locations.update(coord._known_locations)
                if len(locations) == 1:
                    location = next(iter(locations))
                else:
                    raise ServiceValidationError("Multiple locations configured — 'location' parameter is required")

            for entry_data in hass.data[DOMAIN].values():
                c: NjordClient = entry_data["client"]
                await c.push_sensor(kind, location, value, source=source)

        hass.services.async_register(
            DOMAIN,
            SERVICE_PUSH_SENSOR,
            handle_push_sensor,
            schema=SERVICE_PUSH_SENSOR_SCHEMA,
        )

    return True


async def _async_push_sensor(client: NjordClient, kind: str, location: str, value: float, entity_id: str) -> None:
    try:
        await client.push_sensor(kind, location, value, source=entity_id)
    except Exception:
        _LOGGER.warning("Failed to push sensor reading for %s", entity_id, exc_info=True)


async def async_unload_entry(hass: HomeAssistant, entry: ConfigEntry) -> bool:
    """Unload a njord config entry."""
    unload_ok = await hass.config_entries.async_unload_platforms(entry, PLATFORMS)

    if unload_ok:
        entry_data = hass.data[DOMAIN].pop(entry.entry_id)
        coordinator: NjordDataCoordinator = entry_data["coordinator"]
        await coordinator.stop_streams()
        client: NjordClient = entry_data["client"]
        await client.close()

        if not hass.data[DOMAIN]:
            hass.services.async_remove(DOMAIN, SERVICE_TRIGGER_POLL)
            if hass.services.has_service(DOMAIN, SERVICE_PUSH_SENSOR):
                hass.services.async_remove(DOMAIN, SERVICE_PUSH_SENSOR)

    return unload_ok
