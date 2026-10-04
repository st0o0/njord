"""Tests for njord diagnostics."""

from __future__ import annotations

from homeassistant.core import HomeAssistant

from custom_components.njord.diagnostics import async_get_config_entry_diagnostics
from tests.conftest import init_integration


async def test_diagnostics_output_structure(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    """Diagnostics should return the expected top-level keys."""
    await init_integration(hass, mock_config_entry)

    diag = await async_get_config_entry_diagnostics(hass, mock_config_entry)

    assert isinstance(diag, dict)
    assert "config" in diag
    assert "options" in diag
    assert "coordinator" in diag
    assert "stream_states" in diag
    assert "server_status" in diag


async def test_diagnostics_host_redacted(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    """Host should be redacted, port should be visible."""
    await init_integration(hass, mock_config_entry)

    diag = await async_get_config_entry_diagnostics(hass, mock_config_entry)

    assert diag["config"]["host"] == "**REDACTED**"
    assert diag["config"]["port"] == 8081


async def test_diagnostics_server_status(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    """Server status should include the version from the mock."""
    await init_integration(hass, mock_config_entry)

    diag = await async_get_config_entry_diagnostics(hass, mock_config_entry)

    assert diag["server_status"]["version"] == "1.2.3"
    assert diag["server_status"]["uptime_seconds"] == 3600
    assert diag["server_status"]["budget"] is not None
    assert diag["server_status"]["budget"]["usage_percent"] == 25.0
