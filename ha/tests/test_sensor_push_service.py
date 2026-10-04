"""Tests for njord.push_sensor service."""

from __future__ import annotations

from unittest.mock import AsyncMock

import pytest
from homeassistant.core import HomeAssistant
from homeassistant.exceptions import ServiceValidationError

from custom_components.njord.const import DOMAIN
from custom_components.njord.models import SensorPushResult
from tests.conftest import init_integration


async def test_push_sensor_service_registered(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)
    assert hass.services.has_service(DOMAIN, "push_sensor")


async def test_push_sensor_valid_call(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    mock_client.push_sensor = AsyncMock(return_value=SensorPushResult(accepted=True))
    await init_integration(hass, mock_config_entry)

    hass.states.async_set("sensor.wz_temp", "22.5")

    await hass.services.async_call(
        DOMAIN,
        "push_sensor",
        {"kind": "indoor_temperature", "entity_id": "sensor.wz_temp", "location": "home"},
        blocking=True,
    )

    mock_client.push_sensor.assert_called_once_with("indoor_temperature", "home", 22.5, source="sensor.wz_temp")


async def test_push_sensor_source_defaults_to_entity_id(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    mock_client.push_sensor = AsyncMock(return_value=SensorPushResult(accepted=True))
    await init_integration(hass, mock_config_entry)

    hass.states.async_set("sensor.wz_temp", "22.5")

    await hass.services.async_call(
        DOMAIN,
        "push_sensor",
        {"kind": "indoor_temperature", "entity_id": "sensor.wz_temp", "location": "home"},
        blocking=True,
    )

    call_kwargs = mock_client.push_sensor.call_args
    assert call_kwargs.kwargs["source"] == "sensor.wz_temp"


async def test_push_sensor_custom_source(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    mock_client.push_sensor = AsyncMock(return_value=SensorPushResult(accepted=True))
    await init_integration(hass, mock_config_entry)

    hass.states.async_set("sensor.wz_temp", "22.5")

    await hass.services.async_call(
        DOMAIN,
        "push_sensor",
        {
            "kind": "indoor_temperature",
            "entity_id": "sensor.wz_temp",
            "location": "home",
            "source": "wohnzimmer",
        },
        blocking=True,
    )

    call_kwargs = mock_client.push_sensor.call_args
    assert call_kwargs.kwargs["source"] == "wohnzimmer"


async def test_push_sensor_auto_resolve_single_location(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    mock_client.push_sensor = AsyncMock(return_value=SensorPushResult(accepted=True))
    await init_integration(hass, mock_config_entry)

    hass.states.async_set("sensor.wz_temp", "22.5")

    await hass.services.async_call(
        DOMAIN,
        "push_sensor",
        {"kind": "indoor_temperature", "entity_id": "sensor.wz_temp"},
        blocking=True,
    )

    call_args = mock_client.push_sensor.call_args
    assert call_args.args[1] == "home"


async def test_push_sensor_invalid_kind(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    hass.states.async_set("sensor.wz_temp", "22.5")

    with pytest.raises(ServiceValidationError, match="Unknown sensor kind"):
        await hass.services.async_call(
            DOMAIN,
            "push_sensor",
            {"kind": "outdoor_pressure", "entity_id": "sensor.wz_temp", "location": "home"},
            blocking=True,
        )


async def test_push_sensor_entity_not_found(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    with pytest.raises(ServiceValidationError, match="Entity not found"):
        await hass.services.async_call(
            DOMAIN,
            "push_sensor",
            {"kind": "indoor_temperature", "entity_id": "sensor.nonexistent", "location": "home"},
            blocking=True,
        )


async def test_push_sensor_non_numeric_state(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    hass.states.async_set("sensor.wz_temp", "unavailable")

    with pytest.raises(ServiceValidationError, match="not numeric"):
        await hass.services.async_call(
            DOMAIN,
            "push_sensor",
            {"kind": "indoor_temperature", "entity_id": "sensor.wz_temp", "location": "home"},
            blocking=True,
        )
