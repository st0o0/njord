"""Tests for njord trigger poll button."""

from __future__ import annotations

from homeassistant.core import HomeAssistant

from custom_components.njord.button import NjordTriggerPollButton
from custom_components.njord.const import DOMAIN
from tests.conftest import init_integration


async def test_button_belongs_to_server_device(mock_client, mock_config_entry) -> None:
    btn = NjordTriggerPollButton(mock_config_entry, mock_client)
    assert btn.device_info is not None
    ids = btn.device_info["identifiers"]
    assert ids == {(DOMAIN, f"{mock_config_entry.entry_id}_server")}


async def test_button_entity_exists(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    state = hass.states.get("button.server_trigger_poll")
    assert state is not None


async def test_button_press_triggers_poll(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    await hass.services.async_call(
        "button",
        "press",
        {"entity_id": "button.server_trigger_poll"},
        blocking=True,
    )

    mock_client.trigger_poll.assert_awaited_once()


async def test_button_attributes_after_press(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    await hass.services.async_call(
        "button",
        "press",
        {"entity_id": "button.server_trigger_poll"},
        blocking=True,
    )

    state = hass.states.get("button.server_trigger_poll")
    assert state is not None
    assert state.attributes.get("triggered_count") == 6
    assert state.attributes.get("last_triggered") is not None
