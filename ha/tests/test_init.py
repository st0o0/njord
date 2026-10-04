"""Tests for njord integration setup and teardown."""

from __future__ import annotations

from homeassistant.core import HomeAssistant

from custom_components.njord.const import DOMAIN
from tests.conftest import init_integration


async def test_setup_entry(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    assert DOMAIN in hass.data
    assert mock_config_entry.entry_id in hass.data[DOMAIN]

    entry_data = hass.data[DOMAIN][mock_config_entry.entry_id]
    assert "client" in entry_data
    assert "coordinator" in entry_data

    mock_client.connect.assert_awaited_once()
    mock_client.get_catalog.assert_awaited()


async def test_unload_entry(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    result = await hass.config_entries.async_unload(mock_config_entry.entry_id)
    assert result is True
    assert mock_config_entry.entry_id not in hass.data[DOMAIN]
    mock_client.close.assert_awaited_once()


async def test_setup_entry_connection_failure(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    mock_client.connect.side_effect = Exception("Connection refused")

    await hass.config_entries.async_setup(mock_config_entry.entry_id)
    await hass.async_block_till_done()

    assert mock_config_entry.entry_id not in hass.data.get(DOMAIN, {})
