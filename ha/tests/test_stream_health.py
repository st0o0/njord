"""Tests for njord stream health binary sensors."""

from __future__ import annotations

from homeassistant.core import HomeAssistant
from homeassistant.helpers import entity_registry as er

from custom_components.njord.const import DOMAIN
from tests.conftest import init_integration


def _stream_entity_id(hass: HomeAssistant, mock_config_entry, stream_name: str) -> str:
    """Resolve entity_id for a stream binary sensor via unique_id lookup."""
    entity_reg = er.async_get(hass)
    unique_id = f"{mock_config_entry.entry_id}_{stream_name}_stream"
    entity_id = entity_reg.async_get_entity_id("binary_sensor", DOMAIN, unique_id)
    assert entity_id is not None, f"Stream sensor with unique_id={unique_id} not found"
    return entity_id


async def test_stream_sensors_created(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    """All three stream sensors should exist and default to off."""
    await init_integration(hass, mock_config_entry)

    for stream in ("forecast", "enrichment", "config"):
        entity_id = _stream_entity_id(hass, mock_config_entry, stream)
        state = hass.states.get(entity_id)
        assert state is not None, f"Missing stream sensor state for {stream}"
        assert state.state == "off", f"Stream {stream} should be off initially"


async def test_stream_sensor_updates_on_connect(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    """Setting stream state to True and pushing coordinator data should flip sensor to on."""
    await init_integration(hass, mock_config_entry)

    coordinator = hass.data[DOMAIN][mock_config_entry.entry_id]["coordinator"]

    coordinator.stream_states["forecast"] = True
    coordinator.async_set_updated_data(coordinator.data)
    await hass.async_block_till_done()

    entity_id = _stream_entity_id(hass, mock_config_entry, "forecast")
    state = hass.states.get(entity_id)
    assert state is not None
    assert state.state == "on"

    enrichment_id = _stream_entity_id(hass, mock_config_entry, "enrichment")
    state = hass.states.get(enrichment_id)
    assert state is not None
    assert state.state == "off"
