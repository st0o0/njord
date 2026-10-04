"""Tests for njord options flow."""

from __future__ import annotations

from homeassistant.core import HomeAssistant

from custom_components.njord.config_flow import ENRICHMENT_GROUPS
from tests.conftest import init_integration


async def _submit_init_step(hass, entry, **init_overrides):
    """Submit the init step and return the sensors step result."""
    result = await hass.config_entries.options.async_init(entry.entry_id)
    assert result["type"] == "form"
    assert result["step_id"] == "init"

    user_input = {
        "status_poll_interval": entry.options.get("status_poll_interval", 30),
        "enabled_enrichment_groups": ENRICHMENT_GROUPS,
    }
    user_input.update(init_overrides)

    result = await hass.config_entries.options.async_configure(
        result["flow_id"],
        user_input=user_input,
    )
    return result


async def test_options_flow_defaults(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    result = await hass.config_entries.options.async_init(mock_config_entry.entry_id)
    assert result["type"] == "form"
    assert result["step_id"] == "init"

    schema_keys = list(result["data_schema"].schema.keys())
    schema_key_names = [k.schema for k in schema_keys]
    assert "status_poll_interval" in schema_key_names
    assert "enabled_enrichment_groups" in schema_key_names


async def test_options_init_step_navigates_to_sensors(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    result = await _submit_init_step(hass, mock_config_entry)
    assert result["type"] == "form"
    assert result["step_id"] == "sensors"


async def test_options_sensors_step_shows_entity_selectors(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    result = await _submit_init_step(hass, mock_config_entry)
    assert result["step_id"] == "sensors"

    schema_keys = list(result["data_schema"].schema.keys())
    schema_key_names = [k.schema for k in schema_keys]
    assert "home_indoor_temperature" in schema_key_names
    assert "home_indoor_humidity" in schema_key_names


async def test_options_flow_custom_values(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    result = await _submit_init_step(
        hass,
        mock_config_entry,
        status_poll_interval=60,
        enabled_enrichment_groups=[g for g in ENRICHMENT_GROUPS if g != "history"],
    )
    assert result["step_id"] == "sensors"

    result = await hass.config_entries.options.async_configure(
        result["flow_id"],
        user_input={
            "home_indoor_temperature": [],
            "home_indoor_humidity": [],
        },
    )
    assert result["type"] == "create_entry"

    assert mock_config_entry.options["status_poll_interval"] == 60
    assert "history" in mock_config_entry.options["disabled_enrichment_groups"]
    assert "alerts" not in mock_config_entry.options["disabled_enrichment_groups"]


async def test_sensor_push_config_persisted(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    result = await _submit_init_step(hass, mock_config_entry)
    result = await hass.config_entries.options.async_configure(
        result["flow_id"],
        user_input={
            "home_indoor_temperature": ["sensor.wz_temp", "sensor.sz_temp"],
            "home_indoor_humidity": ["sensor.bad_hum"],
        },
    )
    assert result["type"] == "create_entry"

    sensor_push = mock_config_entry.options["sensor_push"]
    assert sensor_push["home"]["indoor_temperature"] == ["sensor.wz_temp", "sensor.sz_temp"]
    assert sensor_push["home"]["indoor_humidity"] == ["sensor.bad_hum"]


async def test_sensor_push_empty_config(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    result = await _submit_init_step(hass, mock_config_entry)
    result = await hass.config_entries.options.async_configure(
        result["flow_id"],
        user_input={
            "home_indoor_temperature": [],
            "home_indoor_humidity": [],
        },
    )
    assert result["type"] == "create_entry"

    sensor_push = mock_config_entry.options["sensor_push"]
    assert sensor_push["home"]["indoor_temperature"] == []
    assert sensor_push["home"]["indoor_humidity"] == []


async def test_options_poll_interval_only_no_reload(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    result = await _submit_init_step(hass, mock_config_entry, status_poll_interval=120)
    result = await hass.config_entries.options.async_configure(
        result["flow_id"],
        user_input={
            "home_indoor_temperature": [],
            "home_indoor_humidity": [],
        },
    )
    assert result["type"] == "create_entry"

    assert mock_config_entry.options["status_poll_interval"] == 120
    assert mock_config_entry.options["disabled_enrichment_groups"] == []
    assert mock_config_entry.state.name == "LOADED"
