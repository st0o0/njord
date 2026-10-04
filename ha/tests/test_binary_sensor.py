"""Tests for njord binary sensor entities."""

from __future__ import annotations

from homeassistant.core import HomeAssistant
from homeassistant.helpers import entity_registry as er

from tests.conftest import init_integration


async def test_inversion_entity_disabled_by_default(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    registry = er.async_get(hass)
    entry = registry.async_get("binary_sensor.home_inversion")
    assert entry is not None
    assert entry.disabled_by == er.RegistryEntryDisabler.INTEGRATION

    state = hass.states.get("binary_sensor.home_inversion")
    assert state is None
