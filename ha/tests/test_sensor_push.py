"""Tests for sensor push state listener."""

from __future__ import annotations

from unittest.mock import AsyncMock

from homeassistant.core import HomeAssistant

from custom_components.njord.const import DOMAIN
from custom_components.njord.models import SensorPushResult
from tests.conftest import init_integration


def _make_entry_with_sensor_push(hass, mock_config_entry, sensor_push: dict):
    from pytest_homeassistant_custom_component.common import MockConfigEntry

    entry = MockConfigEntry(
        domain=DOMAIN,
        data={"host": "localhost", "port": 8081},
        title="njord (localhost)",
        unique_id="localhost:8081",
        options={"sensor_push": sensor_push},
    )
    entry.add_to_hass(hass)
    return entry


async def test_push_on_state_change(hass: HomeAssistant, mock_client) -> None:
    mock_client.push_sensor = AsyncMock(return_value=SensorPushResult(accepted=True))

    entry = _make_entry_with_sensor_push(
        hass,
        None,
        {
            "home": {"indoor_temperature": ["sensor.wz_temp"], "indoor_humidity": []},
        },
    )
    await init_integration(hass, entry)

    hass.states.async_set("sensor.wz_temp", "22.5")
    await hass.async_block_till_done()

    mock_client.push_sensor.assert_called_once_with("indoor_temperature", "home", 22.5, source="sensor.wz_temp")


async def test_push_humidity(hass: HomeAssistant, mock_client) -> None:
    mock_client.push_sensor = AsyncMock(return_value=SensorPushResult(accepted=True))

    entry = _make_entry_with_sensor_push(
        hass,
        None,
        {
            "home": {"indoor_temperature": [], "indoor_humidity": ["sensor.bad_hum"]},
        },
    )
    await init_integration(hass, entry)

    hass.states.async_set("sensor.bad_hum", "65")
    await hass.async_block_till_done()

    mock_client.push_sensor.assert_called_once_with("indoor_humidity", "home", 65.0, source="sensor.bad_hum")


async def test_non_numeric_state_skipped(hass: HomeAssistant, mock_client) -> None:
    mock_client.push_sensor = AsyncMock(return_value=SensorPushResult(accepted=True))

    entry = _make_entry_with_sensor_push(
        hass,
        None,
        {
            "home": {"indoor_temperature": ["sensor.wz_temp"], "indoor_humidity": []},
        },
    )
    await init_integration(hass, entry)

    hass.states.async_set("sensor.wz_temp", "unavailable")
    await hass.async_block_till_done()

    mock_client.push_sensor.assert_not_called()


async def test_unknown_state_skipped(hass: HomeAssistant, mock_client) -> None:
    mock_client.push_sensor = AsyncMock(return_value=SensorPushResult(accepted=True))

    entry = _make_entry_with_sensor_push(
        hass,
        None,
        {
            "home": {"indoor_temperature": ["sensor.wz_temp"], "indoor_humidity": []},
        },
    )
    await init_integration(hass, entry)

    hass.states.async_set("sensor.wz_temp", "unknown")
    await hass.async_block_till_done()

    mock_client.push_sensor.assert_not_called()


async def test_grpc_error_logged_and_dropped(hass: HomeAssistant, mock_client, caplog) -> None:
    mock_client.push_sensor = AsyncMock(side_effect=Exception("connection refused"))

    entry = _make_entry_with_sensor_push(
        hass,
        None,
        {
            "home": {"indoor_temperature": ["sensor.wz_temp"], "indoor_humidity": []},
        },
    )
    await init_integration(hass, entry)

    hass.states.async_set("sensor.wz_temp", "22.5")
    await hass.async_block_till_done()

    assert "Failed to push sensor reading for sensor.wz_temp" in caplog.text


async def test_no_listener_when_config_empty(hass: HomeAssistant, mock_client) -> None:
    mock_client.push_sensor = AsyncMock(return_value=SensorPushResult(accepted=True))

    entry = _make_entry_with_sensor_push(
        hass,
        None,
        {
            "home": {"indoor_temperature": [], "indoor_humidity": []},
        },
    )
    await init_integration(hass, entry)

    hass.states.async_set("sensor.wz_temp", "22.5")
    await hass.async_block_till_done()

    mock_client.push_sensor.assert_not_called()


async def test_no_listener_without_sensor_push_key(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    mock_client.push_sensor = AsyncMock(return_value=SensorPushResult(accepted=True))

    await init_integration(hass, mock_config_entry)

    hass.states.async_set("sensor.wz_temp", "22.5")
    await hass.async_block_till_done()

    mock_client.push_sensor.assert_not_called()
