"""Tests for NjordDataCoordinator."""

from __future__ import annotations

from datetime import UTC, datetime
from unittest.mock import AsyncMock

from homeassistant.core import HomeAssistant

from custom_components.njord.const import DOMAIN
from custom_components.njord.coordinator import NjordCoordinatorData
from tests.conftest import init_integration


async def test_coordinator_fetches_data(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    coordinator = hass.data[DOMAIN][mock_config_entry.entry_id]["coordinator"]
    assert isinstance(coordinator.data, NjordCoordinatorData)
    assert ("home", "icon_d2") in coordinator.data.forecasts
    assert ("home", "ecmwf_ifs025") in coordinator.data.forecasts
    assert "home" in coordinator.data.enrichments


async def test_enrichment_failure_does_not_block_forecasts(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    mock_client.get_enrichments = AsyncMock(side_effect=Exception("enrichment error"))

    await init_integration(hass, mock_config_entry)

    coordinator = hass.data[DOMAIN][mock_config_entry.entry_id]["coordinator"]
    assert ("home", "icon_d2") in coordinator.data.forecasts
    assert "home" not in coordinator.data.enrichments


async def test_forecast_failure_inserts_stub(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    mock_client.get_forecast = AsyncMock(side_effect=Exception("forecast error"))

    await init_integration(hass, mock_config_entry)

    coordinator = hass.data[DOMAIN][mock_config_entry.entry_id]["coordinator"]
    assert ("home", "icon_d2") in coordinator.data.forecasts
    assert ("home", "ecmwf_ifs025") in coordinator.data.forecasts
    stub = coordinator.data.forecasts[("home", "icon_d2")]
    assert stub.updated_at == datetime.min.replace(tzinfo=UTC)
    assert stub.hourly == []
    assert stub.daily == []
    assert "home" in coordinator.data.enrichments
