"""Tests for njord device info registration."""

from __future__ import annotations

from homeassistant.core import HomeAssistant
from homeassistant.helpers import device_registry as dr
from homeassistant.helpers import entity_registry as er

from tests.conftest import init_integration


async def _get_device_for_entity(hass: HomeAssistant, entity_id: str) -> dr.DeviceEntry:
    """Look up the device entry for a given entity_id."""
    entity_reg = er.async_get(hass)
    entity_entry = entity_reg.async_get(entity_id)
    assert entity_entry is not None, f"Entity {entity_id} not found in registry"
    assert entity_entry.device_id is not None, f"Entity {entity_id} has no device_id"

    device_reg = dr.async_get(hass)
    device = device_reg.async_get(entity_entry.device_id)
    assert device is not None, f"Device not found for entity {entity_id}"
    return device


async def test_weather_device_info(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    """Weather entities should belong to a device with model='Weather Station'."""
    await init_integration(hass, mock_config_entry)

    device = await _get_device_for_entity(hass, "weather.home_icon_d2")
    assert device.model == "Weather Station"
    assert device.sw_version == "1.2.3"
    assert device.manufacturer == "njord"


async def test_server_sensor_device_info(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    """Version sensor should belong to a device with model='Weather Service'."""
    await init_integration(hass, mock_config_entry)

    device = await _get_device_for_entity(hass, "sensor.server_version")
    assert device.model == "Weather Service"
    assert device.sw_version == "1.2.3"
    assert device.manufacturer == "njord"


async def test_button_device_info(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    """Trigger poll button should belong to the Server device."""
    await init_integration(hass, mock_config_entry)

    device = await _get_device_for_entity(hass, "button.server_trigger_poll")
    assert device.model == "Weather Service"
    assert device.sw_version == "1.2.3"
    assert device.manufacturer == "njord"
