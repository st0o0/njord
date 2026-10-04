"""Tests for coordinator streaming infrastructure."""

from __future__ import annotations

import asyncio
from datetime import UTC, datetime
from unittest.mock import AsyncMock, MagicMock

from homeassistant.core import HomeAssistant

from custom_components.njord.coordinator import NjordCoordinatorData, NjordDataCoordinator
from custom_components.njord.models import (
    AlertData,
    EnrichmentData,
    ForecastData,
    HourlyForecastData,
    IndexData,
    NjordConfigData,
    NjordLocation,
)


def _make_client() -> AsyncMock:
    client = AsyncMock()
    client.get_config = AsyncMock(
        return_value=NjordConfigData(
            locations=[NjordLocation(name="home", latitude=47.0, longitude=8.0, models=["icon_d2"])],
        )
    )
    client.get_forecast = AsyncMock(
        return_value=ForecastData(
            location="home", model="icon_d2", updated_at=datetime(2024, 1, 1, tzinfo=UTC), hourly=[], daily=[]
        )
    )
    client.get_enrichments = AsyncMock(return_value=EnrichmentData(location="home"))
    return client


async def _make_coordinator(hass: HomeAssistant, client: AsyncMock) -> NjordDataCoordinator:
    coordinator = NjordDataCoordinator(hass, client)
    coordinator.data = NjordCoordinatorData(
        forecasts={
            ("home", "icon_d2"): ForecastData(
                location="home", model="icon_d2", updated_at=datetime(2024, 1, 1, tzinfo=UTC)
            )
        },
        enrichments={"home": EnrichmentData(location="home")},
    )
    coordinator._known_locations = {"home"}
    return coordinator


async def test_coordinator_has_no_update_interval(hass: HomeAssistant) -> None:
    client = _make_client()
    coordinator = NjordDataCoordinator(hass, client)
    assert coordinator.update_interval is None


async def test_forecast_stream_updates_data(hass: HomeAssistant) -> None:
    client = _make_client()
    coordinator = await _make_coordinator(hass, client)

    updated = ForecastData(
        location="home",
        model="icon_d2",
        updated_at=datetime(2024, 2, 1, tzinfo=UTC),
        hourly=[HourlyForecastData(valid_at=datetime(2026, 7, 15, 12, 0, tzinfo=UTC), temperature=25.0)],
    )

    async def fake_stream_forecasts(**kwargs):
        yield updated

    client.stream_forecasts = MagicMock(return_value=fake_stream_forecasts())

    task = hass.async_create_task(coordinator._run_forecast_stream())
    await asyncio.sleep(0.05)
    task.cancel()
    try:
        await task
    except asyncio.CancelledError:
        pass

    assert coordinator.data.forecasts[("home", "icon_d2")].updated_at == datetime(2024, 2, 1, tzinfo=UTC)


async def test_enrichment_stream_merges_correctly(hass: HomeAssistant) -> None:
    client = _make_client()
    coordinator = await _make_coordinator(hass, client)
    coordinator.data.enrichments["home"] = EnrichmentData(
        location="home",
        indices=IndexData(laundry=80),
    )

    alert_event = EnrichmentData(
        location="home",
        alerts=[AlertData(type="storm", severity="red", confidence=1.0)],
    )

    async def fake_stream_enrichments(**kwargs):
        yield alert_event

    client.stream_enrichments = MagicMock(return_value=fake_stream_enrichments())

    task = hass.async_create_task(coordinator._run_enrichment_stream())
    await asyncio.sleep(0.05)
    task.cancel()
    try:
        await task
    except asyncio.CancelledError:
        pass

    result = coordinator.data.enrichments["home"]
    assert result.alerts[0].type == "storm"
    assert result.indices is not None
    assert result.indices.laundry == 80


async def test_config_stream_detects_new_locations(hass: HomeAssistant) -> None:
    client = _make_client()
    coordinator = await _make_coordinator(hass, client)

    new_config = NjordConfigData(
        locations=[
            NjordLocation(name="home", latitude=47.0, longitude=8.0, models=["icon_d2"]),
            NjordLocation(name="bern", latitude=46.9, longitude=7.4, models=["gfs"]),
        ],
    )

    client.get_forecast = AsyncMock(
        return_value=ForecastData(location="bern", model="gfs", updated_at=datetime(2024, 3, 1, tzinfo=UTC))
    )
    client.get_enrichments = AsyncMock(return_value=EnrichmentData(location="bern"))

    factory_called_with: list[NjordLocation] = []

    def mock_factory(location: NjordLocation) -> list:
        factory_called_with.append(location)
        return []

    coordinator.register_entity_factory("weather", MagicMock(), mock_factory)

    async def fake_stream_config():
        yield new_config

    client.stream_config = MagicMock(return_value=fake_stream_config())

    task = hass.async_create_task(coordinator._run_config_stream())
    await asyncio.sleep(0.05)
    task.cancel()
    try:
        await task
    except asyncio.CancelledError:
        pass

    assert "bern" in coordinator._known_locations
    assert ("bern", "gfs") in coordinator.data.forecasts
    assert len(factory_called_with) == 1
    assert factory_called_with[0].name == "bern"


async def test_config_stream_noop_for_known_locations(hass: HomeAssistant) -> None:
    client = _make_client()
    coordinator = await _make_coordinator(hass, client)

    same_config = NjordConfigData(
        locations=[NjordLocation(name="home", latitude=47.0, longitude=8.0, models=["icon_d2"])],
    )

    factory_called: list[str] = []
    coordinator.register_entity_factory("weather", MagicMock(), lambda loc: factory_called.append(loc.name) or [])

    async def fake_stream_config():
        yield same_config

    client.stream_config = MagicMock(return_value=fake_stream_config())

    task = hass.async_create_task(coordinator._run_config_stream())
    await asyncio.sleep(0.05)
    task.cancel()
    try:
        await task
    except asyncio.CancelledError:
        pass

    assert len(factory_called) == 0


async def test_stop_streams_cancels_tasks(hass: HomeAssistant) -> None:
    client = _make_client()
    coordinator = await _make_coordinator(hass, client)

    async def never_ending(**kwargs):
        while True:
            await asyncio.sleep(10)
            yield

    client.stream_forecasts = MagicMock(return_value=never_ending())
    client.stream_enrichments = MagicMock(return_value=never_ending())
    client.stream_config = MagicMock(return_value=never_ending())

    coordinator.start_streams()
    assert len(coordinator._stream_tasks) == 3

    await coordinator.stop_streams()
    assert len(coordinator._stream_tasks) == 0
