"""Tests for streaming lifecycle in integration setup/teardown."""

from __future__ import annotations

from homeassistant.core import HomeAssistant

from custom_components.njord.const import DOMAIN
from custom_components.njord.coordinator import NjordDataCoordinator
from tests.conftest import init_integration


async def test_streams_start_after_setup(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    mock_client.stream_forecasts.assert_called_once()
    mock_client.stream_enrichments.assert_called_once()
    mock_client.stream_config.assert_called_once()


async def test_streams_stop_on_unload(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    coordinator: NjordDataCoordinator = hass.data[DOMAIN][mock_config_entry.entry_id]["coordinator"]
    tasks_before = list(coordinator._stream_tasks)

    result = await hass.config_entries.async_unload(mock_config_entry.entry_id)
    assert result is True

    for task in tasks_before:
        assert task.done()

    mock_client.close.assert_awaited_once()
