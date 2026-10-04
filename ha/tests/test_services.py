"""Tests for njord.trigger_poll service."""

from __future__ import annotations

from homeassistant.core import HomeAssistant

from custom_components.njord.const import DOMAIN
from tests.conftest import init_integration


async def test_service_registered(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)
    assert hass.services.has_service(DOMAIN, "trigger_poll")


async def test_service_call_no_params(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    await hass.services.async_call(DOMAIN, "trigger_poll", {}, blocking=True)

    mock_client.trigger_poll.assert_awaited_once_with(location="", model="")


async def test_service_call_with_location(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    await hass.services.async_call(DOMAIN, "trigger_poll", {"location": "graz"}, blocking=True)

    mock_client.trigger_poll.assert_awaited_once_with(location="graz", model="")


async def test_service_call_with_location_and_model(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    await hass.services.async_call(DOMAIN, "trigger_poll", {"location": "graz", "model": "icon_d2"}, blocking=True)

    mock_client.trigger_poll.assert_awaited_once_with(location="graz", model="icon_d2")


async def test_service_removed_on_unload(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)
    assert hass.services.has_service(DOMAIN, "trigger_poll")

    await hass.config_entries.async_unload(mock_config_entry.entry_id)
    assert not hass.services.has_service(DOMAIN, "trigger_poll")
