"""Tests for dynamic entity creation via config stream."""

from __future__ import annotations

import asyncio
from datetime import UTC, datetime
from unittest.mock import AsyncMock, MagicMock

from homeassistant.core import HomeAssistant

from custom_components.njord.coordinator import NjordCoordinatorData, NjordDataCoordinator
from custom_components.njord.models import (
    EnrichmentData,
    ForecastData,
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
        return_value=ForecastData(location="bern", model="gfs", updated_at=datetime(2024, 1, 1, tzinfo=UTC))
    )
    client.get_enrichments = AsyncMock(return_value=EnrichmentData(location="bern"))
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


async def test_new_location_triggers_factory(hass: HomeAssistant) -> None:
    client = _make_client()
    coordinator = await _make_coordinator(hass, client)

    add_entities = MagicMock()
    entities_created: list[NjordLocation] = []

    def factory(location: NjordLocation) -> list:
        entities_created.append(location)
        return [MagicMock()]

    coordinator.register_entity_factory("weather", add_entities, factory)

    new_config = NjordConfigData(
        locations=[
            NjordLocation(name="home", latitude=47.0, longitude=8.0, models=["icon_d2"]),
            NjordLocation(name="bern", latitude=46.9, longitude=7.4, models=["gfs"]),
        ],
    )

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

    assert len(entities_created) == 1
    assert entities_created[0].name == "bern"
    add_entities.assert_called_once()


async def test_multiple_factories_called(hass: HomeAssistant) -> None:
    client = _make_client()
    coordinator = await _make_coordinator(hass, client)

    weather_add = MagicMock()
    sensor_add = MagicMock()

    coordinator.register_entity_factory("weather", weather_add, lambda loc: [MagicMock()])
    coordinator.register_entity_factory("sensor", sensor_add, lambda loc: [MagicMock()])

    new_config = NjordConfigData(
        locations=[
            NjordLocation(name="home", latitude=47.0, longitude=8.0, models=["icon_d2"]),
            NjordLocation(name="bern", latitude=46.9, longitude=7.4, models=["gfs"]),
        ],
    )

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

    weather_add.assert_called_once()
    sensor_add.assert_called_once()


async def test_duplicate_location_no_double_creation(hass: HomeAssistant) -> None:
    client = _make_client()
    coordinator = await _make_coordinator(hass, client)

    factory_calls: list[str] = []
    coordinator.register_entity_factory("weather", MagicMock(), lambda loc: factory_calls.append(loc.name) or [])

    config_with_bern = NjordConfigData(
        locations=[
            NjordLocation(name="home", latitude=47.0, longitude=8.0, models=["icon_d2"]),
            NjordLocation(name="bern", latitude=46.9, longitude=7.4, models=["gfs"]),
        ],
    )

    async def fake_stream_config():
        yield config_with_bern
        yield config_with_bern

    client.stream_config = MagicMock(return_value=fake_stream_config())

    task = hass.async_create_task(coordinator._run_config_stream())
    await asyncio.sleep(0.05)
    task.cancel()
    try:
        await task
    except asyncio.CancelledError:
        pass

    assert factory_calls.count("bern") == 1
