"""Tests for njord sensor entities."""

from __future__ import annotations

import pytest
from homeassistant.components.sensor import SensorDeviceClass
from homeassistant.core import HomeAssistant
from homeassistant.helpers import entity_registry as er

from tests.conftest import init_integration


async def _setup_with_sensors_enabled(hass, mock_config_entry):
    """Set up integration, enable all disabled sensors, and reload."""
    await init_integration(hass, mock_config_entry)
    registry = er.async_get(hass)
    for entry in list(registry.entities.values()):
        if entry.domain == "sensor" and entry.disabled_by is not None:
            registry.async_update_entity(entry.entity_id, disabled_by=None)
    await hass.config_entries.async_reload(mock_config_entry.entry_id)
    await hass.async_block_till_done()


async def test_index_sensors_exist(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await _setup_with_sensors_enabled(hass, mock_config_entry)

    indices = ["laundry", "outdoor", "running", "cycling", "bbq", "irrigation", "solar", "night_ventilation"]
    for idx in indices:
        state = hass.states.get(f"sensor.home_{idx}_index")
        assert state is not None, f"Missing index sensor: {idx}"


async def test_bbq_index_value(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await _setup_with_sensors_enabled(hass, mock_config_entry)

    state = hass.states.get("sensor.home_bbq_index")
    assert state is not None
    assert state.state == "51"


async def test_vpd_sensor(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await _setup_with_sensors_enabled(hass, mock_config_entry)

    state = hass.states.get("sensor.home_vpd")
    assert state is not None
    assert float(state.state) == pytest.approx(0.59)
    assert state.attributes["category"] == "optimal"


async def test_weather_trend(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await _setup_with_sensors_enabled(hass, mock_config_entry)

    state = hass.states.get("sensor.home_weather_trend")
    assert state is not None
    assert state.state == "Light rain expected in 2 hours"
    assert state.attributes["stability_label"] == "stable"
    assert state.attributes["precip_starts_in_hours"] == 2
    assert state.attributes["reliable_hours"] == 3


async def test_sunshine_pct(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await _setup_with_sensors_enabled(hass, mock_config_entry)

    state = hass.states.get("sensor.home_sunshine")
    assert state is not None
    assert float(state.state) == pytest.approx(66.4)


async def test_diurnal_amplitude(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await _setup_with_sensors_enabled(hass, mock_config_entry)

    state = hass.states.get("sensor.home_diurnal_amplitude")
    assert state is not None
    assert float(state.state) == pytest.approx(7.3)


async def test_model_performance_diagnostic(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await _setup_with_sensors_enabled(hass, mock_config_entry)

    state = hass.states.get("sensor.home_model_performance")
    assert state is not None
    assert float(state.state) == pytest.approx(24.48)
    assert "models" in state.attributes


async def test_frost_hours_sensor(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await _setup_with_sensors_enabled(hass, mock_config_entry)

    state = hass.states.get("sensor.home_frost_hours")
    assert state is not None
    assert state.state == "4"
    assert state.attributes["unit_of_measurement"] == "h"
    assert state.attributes["icon"] == "mdi:snowflake-thermometer"


async def test_frost_confidence_sensor(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await _setup_with_sensors_enabled(hass, mock_config_entry)

    state = hass.states.get("sensor.home_frost_confidence")
    assert state is not None
    assert float(state.state) == pytest.approx(85.0)
    assert state.attributes["unit_of_measurement"] == "%"
    assert state.attributes["icon"] == "mdi:snowflake-check"


async def test_enrichment_sensors_disabled_by_default(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    registry = er.async_get(hass)
    disabled_sensors = [
        "sensor.home_bbq_index",
        "sensor.home_weather_trend",
        "sensor.home_beaufort",
        "sensor.home_wind_chill",
        "sensor.home_dewpoint_comfort",
    ]
    for entity_id in disabled_sensors:
        entry = registry.async_get(entity_id)
        assert entry is not None, f"Missing entity: {entity_id}"
        assert entry.disabled_by == er.RegistryEntryDisabler.INTEGRATION, f"{entity_id} should be disabled by default"
        state = hass.states.get(entity_id)
        assert state is None, f"{entity_id} should have no state when disabled"


# --- Derived Sensor Tests ---


async def test_beaufort_sensor(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await _setup_with_sensors_enabled(hass, mock_config_entry)

    state = hass.states.get("sensor.home_beaufort")
    assert state is not None
    assert state.state == "3"
    assert state.attributes["icon"] == "mdi:windsock"


async def test_wind_chill_sensor(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await _setup_with_sensors_enabled(hass, mock_config_entry)

    state = hass.states.get("sensor.home_wind_chill")
    assert state is not None
    assert float(state.state) == pytest.approx(18.5)
    assert state.attributes.get("device_class") == SensorDeviceClass.TEMPERATURE.value


async def test_dewpoint_comfort_sensor(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await _setup_with_sensors_enabled(hass, mock_config_entry)

    state = hass.states.get("sensor.home_dewpoint_comfort")
    assert state is not None
    assert state.state == "comfortable"
    assert state.attributes["icon"] == "mdi:water-thermometer"


# --- Device Class and Precision Tests ---


async def test_diurnal_amplitude_device_class(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await _setup_with_sensors_enabled(hass, mock_config_entry)

    state = hass.states.get("sensor.home_diurnal_amplitude")
    assert state is not None
    assert state.attributes.get("device_class") == SensorDeviceClass.TEMPERATURE.value


async def test_model_performance_device_class(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await _setup_with_sensors_enabled(hass, mock_config_entry)

    state = hass.states.get("sensor.home_model_performance")
    assert state is not None
    assert state.attributes.get("device_class") == SensorDeviceClass.TEMPERATURE.value


async def test_frost_hours_device_class(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await _setup_with_sensors_enabled(hass, mock_config_entry)

    state = hass.states.get("sensor.home_frost_hours")
    assert state is not None
    assert state.attributes.get("device_class") == SensorDeviceClass.DURATION.value
